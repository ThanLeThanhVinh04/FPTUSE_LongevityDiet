using System.Globalization;
using LongevityDiet.Domain.Entities;
using LongevityDiet.Services.Planning;

namespace LongevityDiet.Services.Tracking;

public static class EatingWindowCalculator
{
    public const int DefaultTargetWindowMinutes = 720;
    public const int MinimumBedtimeGapMinutes = 180;

    public static (DateOnly LocalDate, TimeOnly LocalTime) ToUserLocalDateAndTime(
        DateTimeOffset timestamp,
        string timeZoneId)
    {
        var timeZone = MealPlanningService.ResolveTimeZone(timeZoneId);
        var local = TimeZoneInfo.ConvertTime(timestamp, timeZone);
        return (DateOnly.FromDateTime(local.DateTime), TimeOnly.FromDateTime(local.DateTime));
    }

    public static EatingWindowSnapshot CalculateSnapshot(
        UserScheduleSnapshot schedule,
        DateOnly localDate,
        IReadOnlyList<MealLog> activeLogsOnDate,
        DateTimeOffset calculatedAt)
    {
        var orderedLogs = activeLogsOnDate
            .Where(x => !x.IsDeleted && x.LocalDate == localDate)
            .OrderBy(x => x.EatenAt)
            .ThenBy(x => x.Id)
            .ToList();

        var caloricLogs = orderedLogs
            .Where(x => x.TotalCaloriesKcal > 0)
            .ToList();

        var totalCalories = orderedLogs.Sum(x => x.TotalCaloriesKcal);
        var targetMinutes = schedule.TargetWindowMinutes > 0
            ? schedule.TargetWindowMinutes
            : DefaultTargetWindowMinutes;

        if (caloricLogs.Count == 0)
        {
            return new EatingWindowSnapshot
            {
                Id = Guid.NewGuid(),
                UserId = schedule.UserId,
                LocalDate = localDate,
                TimeZone = schedule.TimeZone,
                LoggedMealCount = orderedLogs.Count,
                CaloricMealCount = 0,
                TotalCaloriesKcal = totalCalories,
                FirstMealAt = null,
                LastMealAt = null,
                FirstMealLocalTime = null,
                LastMealLocalTime = null,
                EatingWindowMinutes = null,
                TargetWindowMinutes = targetMinutes,
                IsWithinTargetWindow = true,
                ConfiguredSleepTime = schedule.SleepTime,
                MinutesBeforeBedtime = null,
                IsLateEatingFlagged = false,
                StatusCode = "NoMeals",
                SummaryMessage = "Chưa có bữa ăn sinh năng lượng nào được ghi nhận trong ngày này.",
                CalculatedAt = calculatedAt,
            };
        }

        var firstMeal = caloricLogs[0];
        var lastMeal = caloricLogs[^1];

        var (_, firstLocalTime) = ToUserLocalDateAndTime(firstMeal.EatenAt, schedule.TimeZone);
        var (_, lastLocalTime) = ToUserLocalDateAndTime(lastMeal.EatenAt, schedule.TimeZone);

        var durationMinutes = Math.Max(
            0,
            (int)Math.Round((lastMeal.EatenAt - firstMeal.EatenAt).TotalMinutes, MidpointRounding.AwayFromZero));

        var isWithinTarget = durationMinutes <= targetMinutes;

        int? minutesBeforeBedtime = null;
        var isLateEating = false;

        if (schedule.SleepTime.HasValue)
        {
            minutesBeforeBedtime = CalculateMinutesBeforeBedtime(
                lastLocalTime,
                schedule.WakeTime,
                schedule.SleepTime.Value);
            isLateEating = minutesBeforeBedtime.Value < MinimumBedtimeGapMinutes;
        }

        var statusCode = ResolveStatusCode(caloricLogs.Count, isWithinTarget, isLateEating);
        var summaryMessage = BuildSummaryMessage(
            caloricLogs.Count,
            durationMinutes,
            targetMinutes,
            isWithinTarget,
            minutesBeforeBedtime,
            isLateEating);

        return new EatingWindowSnapshot
        {
            Id = Guid.NewGuid(),
            UserId = schedule.UserId,
            LocalDate = localDate,
            TimeZone = schedule.TimeZone,
            LoggedMealCount = orderedLogs.Count,
            CaloricMealCount = caloricLogs.Count,
            TotalCaloriesKcal = totalCalories,
            FirstMealAt = firstMeal.EatenAt,
            LastMealAt = lastMeal.EatenAt,
            FirstMealLocalTime = firstLocalTime,
            LastMealLocalTime = lastLocalTime,
            EatingWindowMinutes = durationMinutes,
            TargetWindowMinutes = targetMinutes,
            IsWithinTargetWindow = isWithinTarget,
            ConfiguredSleepTime = schedule.SleepTime,
            MinutesBeforeBedtime = minutesBeforeBedtime,
            IsLateEatingFlagged = isLateEating,
            StatusCode = statusCode,
            SummaryMessage = summaryMessage,
            CalculatedAt = calculatedAt,
        };
    }

    public static int CalculateMinutesBeforeBedtime(
        TimeOnly lastMealLocalTime,
        TimeOnly? wakeTime,
        TimeOnly sleepTime)
    {
        var wakeMinutes = wakeTime.HasValue
            ? (wakeTime.Value.Hour * 60) + wakeTime.Value.Minute
            : 360;

        var sleepMinutes = (sleepTime.Hour * 60) + sleepTime.Minute;
        var lastMealMinutes = (lastMealLocalTime.Hour * 60) + lastMealLocalTime.Minute;

        var sleepIsPastMidnight = sleepMinutes < wakeMinutes || sleepTime.Hour < 4;
        if (sleepIsPastMidnight)
        {
            sleepMinutes += 1440;
            if (lastMealMinutes < wakeMinutes)
            {
                lastMealMinutes += 1440;
            }
        }

        return sleepMinutes - lastMealMinutes;
    }

    private static string ResolveStatusCode(
        int caloricMealCount,
        bool isWithinTarget,
        bool isLateEating)
    {
        if (!isWithinTarget && isLateEating)
        {
            return "WindowExceededAndLateEating";
        }

        if (!isWithinTarget)
        {
            return "WindowExceeded";
        }

        if (isLateEating)
        {
            return "LateEating";
        }

        return caloricMealCount == 1 ? "SingleMeal" : "OnTrack";
    }

    private static string BuildSummaryMessage(
        int caloricMealCount,
        int durationMinutes,
        int targetMinutes,
        bool isWithinTarget,
        int? minutesBeforeBedtime,
        bool isLateEating)
    {
        var hours = durationMinutes / 60;
        var mins = durationMinutes % 60;
        var targetHours = targetMinutes / 60;

        var windowText = caloricMealCount == 1
            ? "Đã ghi nhận 1 bữa ăn trong ngày"
            : string.Create(CultureInfo.InvariantCulture, $"Khung giờ ăn {hours}h {mins:00}m (mục tiêu ≤ {targetHours}h)");

        if (!minutesBeforeBedtime.HasValue)
        {
            return isWithinTarget
                ? $"{windowText} — Đang trong ngưỡng khuyến nghị."
                : $"{windowText} — Vượt khung giờ khuyến nghị.";
        }

        var gap = minutesBeforeBedtime.Value;
        var bedtimeText = gap >= 0
            ? string.Create(CultureInfo.InvariantCulture, $"cách giờ ngủ {gap / 60}h {Math.Abs(gap % 60):00}m")
            : string.Create(CultureInfo.InvariantCulture, $"ăn muộn hơn giờ ngủ {Math.Abs(gap)} phút");

        if (isWithinTarget && !isLateEating)
        {
            return $"{windowText}, {bedtimeText} — Nhịp ăn uống tối ưu.";
        }

        if (!isWithinTarget && isLateEating)
        {
            return $"{windowText}, {bedtimeText} — Cần thu gọn khung giờ ăn và kết thúc bữa tối sớm hơn.";
        }

        return !isWithinTarget
            ? $"{windowText}, {bedtimeText} — Khung giờ ăn dài hơn {targetHours} giờ."
            : $"{windowText}, {bedtimeText} — Nên hoàn tất bữa cuối ít nhất 3 giờ trước khi ngủ.";
    }
}

