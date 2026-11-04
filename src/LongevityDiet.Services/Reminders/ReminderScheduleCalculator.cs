using LongevityDiet.Domain.Entities;

namespace LongevityDiet.Services.Reminders;

public sealed record ReminderScheduleItem(ReminderCategory Category, DateTimeOffset DueAtUtc, string TimeZone);

/// <summary>
/// Local-time scheduler. Handles cross-midnight quiet periods and DST invalid/ambiguous
/// local times without assuming a fixed UTC offset.
/// </summary>
public static class ReminderScheduleCalculator
{
    public static IReadOnlyList<ReminderScheduleItem> Preview(
        ReminderPreference pref, string timeZoneId, DateTimeOffset now)
    {
        if (!pref.OptedIn) return [];
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var candidates = new List<ReminderScheduleItem>();
        if (pref.MealLoggingEnabled)
            candidates.Add(Next(ReminderCategory.MealLogging, pref.MealTime, pref, zone, now));
        if (pref.ActivityEnabled)
            candidates.Add(Next(ReminderCategory.Activity, pref.ActivityTime, pref, zone, now));
        if (pref.ChallengeEnabled)
            candidates.Add(Next(ReminderCategory.Challenge, pref.ChallengeTime, pref, zone, now));
        if (pref.WeeklySummaryEnabled)
            candidates.Add(Next(ReminderCategory.WeeklySummary, pref.WeeklySummaryTime, pref, zone, now));
        return candidates.OrderBy(x => x.DueAtUtc).ToArray();
    }

    public static ReminderScheduleItem Next(
        ReminderCategory category, TimeOnly localTime,
        ReminderPreference pref, TimeZoneInfo zone, DateTimeOffset now)
    {
        var localNow = TimeZoneInfo.ConvertTime(now, zone);
        var firstDate = DateOnly.FromDateTime(localNow.DateTime);
        for (var day = 0; day < 15; day++)
        {
            var date = firstDate.AddDays(day);
            if (category == ReminderCategory.WeeklySummary &&
                date.DayOfWeek != DayOfWeek.Monday)
                continue;
            var desired = date.ToDateTime(localTime, DateTimeKind.Unspecified);
            var quiet = InQuietHours(localTime, pref.QuietStart, pref.QuietEnd);
            if (quiet)
            {
                var shiftToNextDay = pref.QuietStart > pref.QuietEnd &&
                    localTime >= pref.QuietStart;
                desired = date.AddDays(shiftToNextDay ? 1 : 0)
                    .ToDateTime(pref.QuietEnd, DateTimeKind.Unspecified);
            }
            while (zone.IsInvalidTime(desired))
                desired = desired.AddMinutes(1);
            var offset = zone.IsAmbiguousTime(desired)
                ? zone.GetAmbiguousTimeOffsets(desired).Max()
                : zone.GetUtcOffset(desired);
            var due = new DateTimeOffset(desired, offset).ToUniversalTime();
            if (due > now)
                return new ReminderScheduleItem(category, due, zone.Id);
        }

        throw new InvalidOperationException("No valid reminder instant was found within 15 days.");
    }

    public static bool InQuietHours(TimeOnly value, TimeOnly start, TimeOnly end)
    {
        if (start == end) return false; // same time means no quiet interval
        return start < end
            ? value >= start && value < end
            : value >= start || value < end;
    }
}
