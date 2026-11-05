using LongevityDiet.Domain.Entities;
using LongevityDiet.Repositories.Repositories;
using LongevityDiet.Services.Identity;

namespace LongevityDiet.Services.Tracking;

public sealed class ChallengeProgressService(
    IChallengeProgressRepository repository,
    IMealLogRepository mealLogRepository,
    IEatingWindowRepository eatingWindowRepository,
    ITrackingUserScheduleProvider scheduleProvider,
    TimeProvider timeProvider)
{
    public async Task<ServiceResult<IReadOnlyList<ChallengeTemplateSummary>>> ListTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var templates = await repository.ListActiveTemplatesAsync(cancellationToken);
        return ServiceResult.Success<IReadOnlyList<ChallengeTemplateSummary>>(templates.Select(ToTemplateSummary).ToList());
    }

    public async Task<ServiceResult<UserChallengeSummary>> StartAsync(
        Guid userId,
        Guid templateId,
        DateOnly? requestedStartDate,
        CancellationToken cancellationToken = default)
    {
        if (await repository.GetActiveChallengeAsync(userId, cancellationToken) is not null)
            return ServiceResult.Failure<UserChallengeSummary>("active_challenge_exists", "An active challenge already exists.");

        var template = await repository.GetTemplateAsync(templateId, cancellationToken);
        if (template is null)
            return ServiceResult.Failure<UserChallengeSummary>("challenge_template_not_found", "Challenge template was not found.");

        var schedule = await scheduleProvider.GetUserScheduleAsync(userId, cancellationToken);
        if (schedule is null)
            return ServiceResult.Failure<UserChallengeSummary>("user_not_found", "User was not found.");

        var now = timeProvider.GetUtcNow();
        var today = ToLocalDate(now, schedule.TimeZone);
        var startDate = requestedStartDate ?? today;
        if (startDate < today.AddDays(-1) || startDate > today.AddDays(1))
            return ServiceResult.Failure<UserChallengeSummary>("invalid_start_date", "Challenge start date must be today or within one day of today.");

        var challenge = new UserChallenge
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ChallengeTemplateId = template.Id,
            TimeZone = schedule.TimeZone,
            StartDate = startDate,
            EndDate = startDate.AddDays(template.DurationDays - 1),
            Status = UserChallengeStatus.Active,
            StartedAt = now,
            UpdatedAt = now,
            ChallengeTemplate = template,
        };

        foreach (var day in template.Days.OrderBy(x => x.DayNumber))
        {
            challenge.Days.Add(new UserChallengeDay
            {
                Id = Guid.NewGuid(),
                UserChallengeId = challenge.Id,
                DayNumber = day.DayNumber,
                LocalDate = startDate.AddDays(day.DayNumber - 1),
                IsCompleted = false,
                UpdatedAt = now,
            });
        }

        await repository.AddChallengeAsync(challenge, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return ServiceResult.Success(ToSummary(challenge));
    }

    public async Task<ServiceResult<UserChallengeSummary>> GetCurrentAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var challenge = await repository.GetActiveChallengeAsync(userId, cancellationToken);
        return challenge is null
            ? ServiceResult.Failure<UserChallengeSummary>("challenge_not_found", "No active challenge was found.")
            : ServiceResult.Success(ToSummary(challenge));
    }

    public async Task<ServiceResult<IReadOnlyList<UserChallengeSummary>>> ListHistoryAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var items = await repository.ListHistoryAsync(userId, cancellationToken);
        return ServiceResult.Success<IReadOnlyList<UserChallengeSummary>>(items.Select(ToSummary).ToList());
    }

    public async Task<ServiceResult<UserChallengeSummary>> SetDayCompletionAsync(
        Guid userId,
        Guid challengeId,
        int dayNumber,
        bool completed,
        CancellationToken cancellationToken = default)
    {
        var challenge = await repository.GetChallengeAsync(challengeId, userId, cancellationToken);
        if (challenge is null)
            return ServiceResult.Failure<UserChallengeSummary>("challenge_not_found", "Challenge was not found.");
        if (challenge.Status == UserChallengeStatus.Abandoned)
            return ServiceResult.Failure<UserChallengeSummary>("challenge_not_active", "Challenge is not active.");
        if (dayNumber < 1 || dayNumber > challenge.Days.Count)
            return ServiceResult.Failure<UserChallengeSummary>("invalid_challenge_day", "Challenge day is out of range.");

        var day = challenge.Days.Single(x => x.DayNumber == dayNumber);
        var now = timeProvider.GetUtcNow();
        var localToday = ToLocalDate(now, challenge.TimeZone);
        if (day.LocalDate > localToday)
            return ServiceResult.Failure<UserChallengeSummary>("future_challenge_day", "A future challenge day cannot be completed.");

        if (day.IsCompleted != completed)
        {
            day.IsCompleted = completed;
            day.CompletedAt = completed ? now : null;
            day.UpdatedAt = now;
        }

        var allCompleted = challenge.Days.All(x => x.IsCompleted);
        challenge.Status = allCompleted ? UserChallengeStatus.Completed : UserChallengeStatus.Active;
        challenge.CompletedAt = allCompleted ? (challenge.CompletedAt ?? now) : null;
        challenge.UpdatedAt = now;

        await repository.SaveChangesAsync(cancellationToken);
        await RebuildDailySummaryAsync(userId, day.LocalDate, cancellationToken);
        return ServiceResult.Success(ToSummary(challenge));
    }

    public async Task<ServiceResult<DashboardSummary>> GetDashboardAsync(
        Guid userId,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken cancellationToken = default)
    {
        var schedule = await scheduleProvider.GetUserScheduleAsync(userId, cancellationToken);
        if (schedule is null)
            return ServiceResult.Failure<DashboardSummary>("user_not_found", "User was not found.");

        var now = timeProvider.GetUtcNow();
        var today = ToLocalDate(now, schedule.TimeZone);
        var end = toDate ?? today;
        var start = fromDate ?? end.AddDays(-13);
        if (end < start || end.DayNumber - start.DayNumber > 30)
            return ServiceResult.Failure<DashboardSummary>("invalid_date_range", "Date range must be valid and at most 31 days.");

        for (var date = start; date <= end; date = date.AddDays(1))
            await RebuildDailySummaryAsync(userId, date, cancellationToken);

        var summaries = await repository.ListDailySummariesAsync(userId, start, end, cancellationToken);
        var active = await repository.GetActiveChallengeAsync(userId, cancellationToken);
        var history = active is null ? await repository.ListHistoryAsync(userId, cancellationToken) : [];
        var current = active ?? (history.Count > 0 ? history[0] : null);

        var trend = summaries.Select(x => new DashboardDayPoint(
            x.LocalDate,
            x.LoggedMealCount,
            x.TotalCaloriesKcal,
            x.EatingWindowWithinTarget,
            x.ChallengeDayCompleted,
            x.ChallengeCurrentStreak,
            x.LoggingConsistencyScore)).ToList();

        var latest = summaries.OrderByDescending(x => x.LocalDate).FirstOrDefault(x => x.LoggedMealCount > 0 || x.EatingWindowWithinTarget.HasValue);
        var loggedDays = summaries.Count(x => x.LoggedMealCount > 0);
        var totalDays = end.DayNumber - start.DayNumber + 1;

        return ServiceResult.Success(new DashboardSummary(
            start,
            end,
            schedule.TimeZone,
            loggedDays,
            totalDays == 0 ? 0 : decimal.Round(loggedDays * 100m / totalDays, 1),
            latest?.TotalCaloriesKcal,
            latest?.EatingWindowWithinTarget,
            current?.Days.Count(x => x.IsCompleted) ?? 0,
            current is null ? 0 : CalculateStreak(current),
            "unavailable:no-activity-source",
            "unavailable:no-ldas-source",
            now,
            trend));
    }

    public async Task<ServiceResult<bool>> ApplyProjectionEventAsync(
        ProgressProjectionInput input,
        CancellationToken cancellationToken = default)
    {
        if (await repository.HasProcessedEventAsync(input.EventId, cancellationToken))
            return ServiceResult.Success(false);

        await RebuildDailySummaryAsync(input.UserId, input.LocalDate, cancellationToken);
        await repository.AddProcessedEventAsync(new ProgressEventReceipt
        {
            EventId = input.EventId,
            EventType = input.EventType,
            ProcessedAt = timeProvider.GetUtcNow(),
        }, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return ServiceResult.Success(true);
    }

    public async Task RebuildDailySummaryAsync(Guid userId, DateOnly localDate, CancellationToken cancellationToken = default)
    {
        var logs = await mealLogRepository.ListActiveByLocalDateAsync(userId, localDate, cancellationToken);
        var window = await eatingWindowRepository.GetByUserAndDateAsync(userId, localDate, cancellationToken);
        var challenge = await repository.GetActiveChallengeAsync(userId, cancellationToken);
        challenge ??= (await repository.ListHistoryAsync(userId, cancellationToken))
            .FirstOrDefault(x => localDate >= x.StartDate && localDate <= x.EndDate);

        var day = challenge?.Days.FirstOrDefault(x => x.LocalDate == localDate);
        var logged = logs.Count > 0;

        await repository.UpsertDailySummaryAsync(new DailyWellnessSummary
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            LocalDate = localDate,
            LoggedMealCount = logs.Count,
            TotalCaloriesKcal = logs.Sum(x => x.TotalCaloriesKcal),
            EatingWindowWithinTarget = window is null ? null : window.IsWithinTargetWindow,
            ChallengeDayCompleted = day?.IsCompleted ?? false,
            ChallengeCurrentStreak = challenge is null ? 0 : CalculateStreak(challenge, localDate),
            LoggingConsistencyScore = logged ? 1m : 0m,
            DataAvailability = "tracking,challenge;activity=unavailable;ldas=unavailable",
            UpdatedAt = timeProvider.GetUtcNow(),
        }, cancellationToken);

        await repository.SaveChangesAsync(cancellationToken);
    }

    private static ChallengeTemplateSummary ToTemplateSummary(ChallengeTemplate template) =>
        new(template.Id, template.Code, template.Title, template.Description, template.DurationDays,
            template.Days.OrderBy(x => x.DayNumber)
                .Select(x => new ChallengeDayTemplateSummary(x.DayNumber, x.Title, x.Guidance)).ToList());

    private static UserChallengeSummary ToSummary(UserChallenge challenge)
    {
        var templateDays = challenge.ChallengeTemplate.Days.ToDictionary(x => x.DayNumber);
        var ordered = challenge.Days.OrderBy(x => x.DayNumber).ToList();
        var completed = ordered.Count(x => x.IsCompleted);
        return new UserChallengeSummary(
            challenge.Id,
            challenge.ChallengeTemplateId,
            challenge.ChallengeTemplate.Code,
            challenge.ChallengeTemplate.Title,
            challenge.TimeZone,
            challenge.StartDate,
            challenge.EndDate,
            challenge.Status.ToString(),
            completed,
            CalculateStreak(challenge),
            challenge.Days.Count == 0 ? 0 : completed * 100 / challenge.Days.Count,
            challenge.StartedAt,
            challenge.CompletedAt,
            ordered.Select(x =>
            {
                var templateDay = templateDays[x.DayNumber];
                return new ChallengeDaySummary(x.Id, x.DayNumber, x.LocalDate, templateDay.Title, templateDay.Guidance, x.IsCompleted, x.CompletedAt);
            }).ToList());
    }

    private static int CalculateStreak(UserChallenge challenge, DateOnly? throughDate = null)
    {
        var completed = challenge.Days.Where(x => x.IsCompleted && (!throughDate.HasValue || x.LocalDate <= throughDate.Value))
            .OrderByDescending(x => x.LocalDate).ToList();
        if (completed.Count == 0) return 0;

        var expected = completed[0].LocalDate;
        var streak = 0;
        foreach (var day in completed)
        {
            if (day.LocalDate != expected) break;
            streak++;
            expected = expected.AddDays(-1);
        }
        return streak;
    }

    private static DateOnly ToLocalDate(DateTimeOffset instant, string timeZoneId)
    {
        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);
        }
        catch (TimeZoneNotFoundException)
        {
            return DateOnly.FromDateTime(instant.UtcDateTime);
        }
        catch (InvalidTimeZoneException)
        {
            return DateOnly.FromDateTime(instant.UtcDateTime);
        }
    }
}
