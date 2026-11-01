using LongevityDiet.Domain.Entities;
using LongevityDiet.Repositories.Repositories;
using LongevityDiet.Services.Identity;

namespace LongevityDiet.Services.Tracking;

public sealed class MealTrackingService(
    IMealLogRepository mealLogRepository,
    IEatingWindowRepository eatingWindowRepository,
    ITrackingUserScheduleProvider scheduleProvider,
    TimeProvider timeProvider)
{
    public async Task<ServiceResult<MealLogSummary>> CreateMealLogAsync(
        Guid userId,
        CreateMealLogCommand command,
        CancellationToken cancellationToken = default)
    {
        var schedule = await scheduleProvider.GetUserScheduleAsync(userId, cancellationToken);
        if (schedule is null)
        {
            return ServiceResult.Failure<MealLogSummary>(
                "user_not_found",
                "User was not found.");
        }

        var validationError = ValidateMealLogInput(command.Title, command.MealType, command.Items, out var slotType);
        if (validationError is not null)
        {
            return ServiceResult.Failure<MealLogSummary>(
                validationError.Value.Code,
                validationError.Value.Message);
        }

        var now = timeProvider.GetUtcNow();
        var (localDate, _) = EatingWindowCalculator.ToUserLocalDateAndTime(command.EatenAt, schedule.TimeZone);

        var mealLog = new MealLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PlannedMealId = command.PlannedMealId,
            EatenAt = command.EatenAt,
            LocalDate = localDate,
            TimeZone = schedule.TimeZone,
            MealType = slotType,
            Title = command.Title.Trim(),
            Notes = string.IsNullOrWhiteSpace(command.Notes) ? null : command.Notes.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
        };

        var items = BuildLogItems(mealLog.Id, command.Items);
        foreach (var item in items)
        {
            mealLog.Items.Add(item);
        }

        ApplyAggregateTotals(mealLog, items);

        await mealLogRepository.AddAsync(mealLog, cancellationToken);
        await mealLogRepository.SaveChangesAsync(cancellationToken);

        await RecalculateAndPersistSnapshotAsync(schedule, localDate, now, cancellationToken);

        return ServiceResult.Success(ToSummary(mealLog));
    }

    public async Task<ServiceResult<MealLogSummary>> UpdateMealLogAsync(
        Guid userId,
        Guid mealLogId,
        UpdateMealLogCommand command,
        CancellationToken cancellationToken = default)
    {
        var schedule = await scheduleProvider.GetUserScheduleAsync(userId, cancellationToken);
        if (schedule is null)
        {
            return ServiceResult.Failure<MealLogSummary>(
                "user_not_found",
                "User was not found.");
        }

        var mealLog = await mealLogRepository.GetByIdForUserAsync(mealLogId, userId, cancellationToken);
        if (mealLog is null)
        {
            return ServiceResult.Failure<MealLogSummary>(
                "meal_log_not_found",
                "Meal log was not found.");
        }

        var validationError = ValidateMealLogInput(command.Title, command.MealType, command.Items, out var slotType);
        if (validationError is not null)
        {
            return ServiceResult.Failure<MealLogSummary>(
                validationError.Value.Code,
                validationError.Value.Message);
        }

        var now = timeProvider.GetUtcNow();
        var previousLocalDate = mealLog.LocalDate;
        var (newLocalDate, _) = EatingWindowCalculator.ToUserLocalDateAndTime(command.EatenAt, schedule.TimeZone);

        mealLog.PlannedMealId = command.PlannedMealId;
        mealLog.EatenAt = command.EatenAt;
        mealLog.LocalDate = newLocalDate;
        mealLog.TimeZone = schedule.TimeZone;
        mealLog.MealType = slotType;
        mealLog.Title = command.Title.Trim();
        mealLog.Notes = string.IsNullOrWhiteSpace(command.Notes) ? null : command.Notes.Trim();
        mealLog.UpdatedAt = now;

        var newItems = BuildLogItems(mealLog.Id, command.Items);
        mealLogRepository.ReplaceItems(mealLog, newItems);
        ApplyAggregateTotals(mealLog, newItems);

        await mealLogRepository.SaveChangesAsync(cancellationToken);

        await RecalculateAndPersistSnapshotAsync(schedule, newLocalDate, now, cancellationToken);
        if (previousLocalDate != newLocalDate)
        {
            await RecalculateAndPersistSnapshotAsync(schedule, previousLocalDate, now, cancellationToken);
        }

        return ServiceResult.Success(ToSummary(mealLog));
    }

    public async Task<ServiceResult<bool>> DeleteMealLogAsync(
        Guid userId,
        Guid mealLogId,
        CancellationToken cancellationToken = default)
    {
        var schedule = await scheduleProvider.GetUserScheduleAsync(userId, cancellationToken);
        if (schedule is null)
        {
            return ServiceResult.Failure<bool>(
                "user_not_found",
                "User was not found.");
        }

        var mealLog = await mealLogRepository.GetByIdForUserAsync(mealLogId, userId, cancellationToken);
        if (mealLog is null)
        {
            return ServiceResult.Failure<bool>(
                "meal_log_not_found",
                "Meal log was not found.");
        }

        var now = timeProvider.GetUtcNow();
        var affectedDate = mealLog.LocalDate;

        mealLog.IsDeleted = true;
        mealLog.DeletedAt = now;
        mealLog.UpdatedAt = now;

        await mealLogRepository.SaveChangesAsync(cancellationToken);
        await RecalculateAndPersistSnapshotAsync(schedule, affectedDate, now, cancellationToken);

        return ServiceResult.Success(true);
    }

    public async Task<ServiceResult<IReadOnlyList<MealLogSummary>>> ListMealLogsAsync(
        Guid userId,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken cancellationToken = default)
    {
        var schedule = await scheduleProvider.GetUserScheduleAsync(userId, cancellationToken);
        if (schedule is null)
        {
            return ServiceResult.Failure<IReadOnlyList<MealLogSummary>>(
                "user_not_found",
                "User was not found.");
        }

        var (todayLocal, _) = EatingWindowCalculator.ToUserLocalDateAndTime(
            timeProvider.GetUtcNow(),
            schedule.TimeZone);

        var effectiveFrom = fromDate ?? todayLocal.AddDays(-6);
        var effectiveTo = toDate ?? todayLocal;

        if (effectiveTo < effectiveFrom)
        {
            return ServiceResult.Failure<IReadOnlyList<MealLogSummary>>(
                "invalid_date_range",
                "End date must be greater than or equal to start date.");
        }

        if (effectiveTo.DayNumber - effectiveFrom.DayNumber > 90)
        {
            return ServiceResult.Failure<IReadOnlyList<MealLogSummary>>(
                "invalid_date_range",
                "Date range cannot exceed 90 days.");
        }

        var logs = await mealLogRepository.ListByDateRangeAsync(
            userId,
            effectiveFrom,
            effectiveTo,
            cancellationToken);

        var summaries = logs.Select(ToSummary).ToList();
        return ServiceResult.Success<IReadOnlyList<MealLogSummary>>(summaries);
    }

    public async Task<ServiceResult<EatingWindowSummary>> GetEatingWindowSummaryAsync(
        Guid userId,
        DateOnly? date,
        CancellationToken cancellationToken = default)
    {
        var schedule = await scheduleProvider.GetUserScheduleAsync(userId, cancellationToken);
        if (schedule is null)
        {
            return ServiceResult.Failure<EatingWindowSummary>(
                "user_not_found",
                "User was not found.");
        }

        var now = timeProvider.GetUtcNow();
        var (todayLocal, _) = EatingWindowCalculator.ToUserLocalDateAndTime(now, schedule.TimeZone);
        var targetDate = date ?? todayLocal;

        var snapshot = await RecalculateAndPersistSnapshotAsync(schedule, targetDate, now, cancellationToken);
        return ServiceResult.Success(ToWindowSummary(snapshot));
    }

    public async Task<ServiceResult<IReadOnlyList<EatingWindowSummary>>> ListEatingWindowSummariesAsync(
        Guid userId,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken cancellationToken = default)
    {
        var schedule = await scheduleProvider.GetUserScheduleAsync(userId, cancellationToken);
        if (schedule is null)
        {
            return ServiceResult.Failure<IReadOnlyList<EatingWindowSummary>>(
                "user_not_found",
                "User was not found.");
        }

        var now = timeProvider.GetUtcNow();
        var (todayLocal, _) = EatingWindowCalculator.ToUserLocalDateAndTime(now, schedule.TimeZone);
        var effectiveFrom = fromDate ?? todayLocal.AddDays(-6);
        var effectiveTo = toDate ?? todayLocal;

        if (effectiveTo < effectiveFrom || effectiveTo.DayNumber - effectiveFrom.DayNumber > 31)
        {
            return ServiceResult.Failure<IReadOnlyList<EatingWindowSummary>>(
                "invalid_date_range",
                "Date range must be valid and at most 31 days.");
        }

        var logs = await mealLogRepository.ListByDateRangeAsync(
            userId,
            effectiveFrom,
            effectiveTo,
            cancellationToken);

        var logsByDate = logs
            .GroupBy(x => x.LocalDate)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<MealLog>)g.ToList());

        var result = new List<EatingWindowSummary>();
        var totalDays = effectiveTo.DayNumber - effectiveFrom.DayNumber + 1;

        for (var i = 0; i < totalDays; i++)
        {
            var currentDate = effectiveFrom.AddDays(i);
            var dayLogs = logsByDate.GetValueOrDefault(currentDate, []);
            var snapshot = EatingWindowCalculator.CalculateSnapshot(schedule, currentDate, dayLogs, now);
            result.Add(ToWindowSummary(snapshot));
        }

        return ServiceResult.Success<IReadOnlyList<EatingWindowSummary>>(result);
    }

    private async Task<EatingWindowSnapshot> RecalculateAndPersistSnapshotAsync(
        UserScheduleSnapshot schedule,
        DateOnly localDate,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var activeLogs = await mealLogRepository.ListActiveByLocalDateAsync(
            schedule.UserId,
            localDate,
            cancellationToken);

        var snapshot = EatingWindowCalculator.CalculateSnapshot(schedule, localDate, activeLogs, now);
        await eatingWindowRepository.UpsertSnapshotAsync(snapshot, cancellationToken);
        await eatingWindowRepository.SaveChangesAsync(cancellationToken);
        return snapshot;
    }

    private static (string Code, string Message)? ValidateMealLogInput(
        string title,
        string mealType,
        IReadOnlyList<MealLogItemInput> items,
        out MealSlotType slotType)
    {
        slotType = MealSlotType.Breakfast;

        if (string.IsNullOrWhiteSpace(title))
        {
            return ("invalid_meal_log", "Meal title is required.");
        }

        if (!Enum.TryParse(mealType, ignoreCase: true, out slotType) ||
            !Enum.IsDefined(slotType))
        {
            return ("invalid_meal_type", "Meal type must be Breakfast, Lunch, Dinner, or Snack.");
        }

        if (items is null || items.Count == 0)
        {
            return ("invalid_meal_items", "At least one meal item is required.");
        }

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.FoodName))
            {
                return ("invalid_meal_items", "Each meal item must have a food name.");
            }

            if (item.ServingAmount <= 0 || item.ServingAmount > 5000)
            {
                return ("invalid_meal_items", "Serving amount must be greater than zero.");
            }

            if (item.CaloriesKcal is < 0 or > 5000)
            {
                return ("invalid_meal_items", "Calories must be between 0 and 5000 kcal.");
            }
        }

        return null;
    }

    private static List<MealLogItem> BuildLogItems(
        Guid mealLogId,
        IReadOnlyList<MealLogItemInput> inputs)
    {
        return inputs
            .Select(x => new MealLogItem
            {
                Id = Guid.NewGuid(),
                MealLogId = mealLogId,
                FoodCode = string.IsNullOrWhiteSpace(x.FoodCode) ? null : x.FoodCode.Trim(),
                FoodName = x.FoodName.Trim(),
                ServingAmount = decimal.Round(x.ServingAmount, 2),
                ServingUnit = string.IsNullOrWhiteSpace(x.ServingUnit) ? "khẩu phần" : x.ServingUnit.Trim(),
                CaloriesKcal = x.CaloriesKcal,
                ProteinGrams = decimal.Round(Math.Max(0m, x.ProteinGrams), 2),
                CarbsGrams = decimal.Round(Math.Max(0m, x.CarbsGrams), 2),
                FatGrams = decimal.Round(Math.Max(0m, x.FatGrams), 2),
                FiberGrams = decimal.Round(Math.Max(0m, x.FiberGrams), 2),
                IsPlantBased = x.IsPlantBased,
            })
            .ToList();
    }

    private static void ApplyAggregateTotals(MealLog mealLog, IReadOnlyList<MealLogItem> items)
    {
        mealLog.TotalCaloriesKcal = items.Sum(x => x.CaloriesKcal);
        mealLog.TotalProteinGrams = decimal.Round(items.Sum(x => x.ProteinGrams), 2);
        mealLog.TotalCarbsGrams = decimal.Round(items.Sum(x => x.CarbsGrams), 2);
        mealLog.TotalFatGrams = decimal.Round(items.Sum(x => x.FatGrams), 2);
        mealLog.TotalFiberGrams = decimal.Round(items.Sum(x => x.FiberGrams), 2);
    }

    private static MealLogSummary ToSummary(MealLog log)
    {
        var (_, localTime) = EatingWindowCalculator.ToUserLocalDateAndTime(log.EatenAt, log.TimeZone);
        var items = log.Items
            .Select(x => new MealLogItemSummary(
                x.Id,
                x.FoodCode,
                x.FoodName,
                x.ServingAmount,
                x.ServingUnit,
                x.CaloriesKcal,
                x.ProteinGrams,
                x.CarbsGrams,
                x.FatGrams,
                x.FiberGrams,
                x.IsPlantBased))
            .ToList();

        return new MealLogSummary(
            log.Id,
            log.UserId,
            log.PlannedMealId,
            log.EatenAt,
            log.LocalDate,
            localTime,
            log.TimeZone,
            log.MealType.ToString(),
            log.Title,
            log.Notes,
            log.TotalCaloriesKcal,
            log.TotalProteinGrams,
            log.TotalCarbsGrams,
            log.TotalFatGrams,
            log.TotalFiberGrams,
            log.CreatedAt,
            log.UpdatedAt,
            items);
    }

    private static EatingWindowSummary ToWindowSummary(EatingWindowSnapshot snapshot) =>
        new(
            snapshot.UserId,
            snapshot.LocalDate,
            snapshot.TimeZone,
            snapshot.LoggedMealCount,
            snapshot.CaloricMealCount,
            snapshot.TotalCaloriesKcal,
            snapshot.FirstMealAt,
            snapshot.LastMealAt,
            snapshot.FirstMealLocalTime,
            snapshot.LastMealLocalTime,
            snapshot.EatingWindowMinutes,
            snapshot.TargetWindowMinutes,
            snapshot.IsWithinTargetWindow,
            snapshot.ConfiguredSleepTime,
            snapshot.MinutesBeforeBedtime,
            snapshot.IsLateEatingFlagged,
            snapshot.StatusCode,
            snapshot.SummaryMessage,
            snapshot.CalculatedAt);
}

