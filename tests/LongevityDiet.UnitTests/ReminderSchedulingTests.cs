using LongevityDiet.Domain.Entities;
using LongevityDiet.Services.Reminders;

namespace LongevityDiet.UnitTests;

public sealed class ReminderSchedulingTests
{
    [Fact]
    public void QuietHoursAcrossMidnight_SendsAtNextSafeLocalTime()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        var preference = new ReminderPreference
        {
            OptedIn = true,
            ActivityEnabled = true,
            ActivityTime = new TimeOnly(23, 30),
            QuietStart = new TimeOnly(22, 0),
            QuietEnd = new TimeOnly(7, 0),
        };
        var now = new DateTimeOffset(2026, 11, 5, 10, 0, 0, TimeSpan.FromHours(7));
        var item = ReminderScheduleCalculator.Next(
            ReminderCategory.Activity, preference.ActivityTime, preference, zone, now);
        var local = TimeZoneInfo.ConvertTime(item.DueAtUtc, zone);
        Assert.Equal(new TimeOnly(7, 0), TimeOnly.FromDateTime(local.DateTime));
        Assert.Equal(new DateOnly(2026, 11, 6), DateOnly.FromDateTime(local.DateTime));
    }

    [Fact]
    public void SpringForward_InvalidLocalTimeRollsToNextValidMinute()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var preference = new ReminderPreference
        {
            OptedIn = true,
            ActivityTime = new TimeOnly(2, 30),
            QuietStart = new TimeOnly(6, 0), // equal start/end disables quiet mode for DST tests
            QuietEnd = new TimeOnly(6, 0),
        };
        var now = new DateTimeOffset(2026, 3, 8, 0, 30, 0, TimeSpan.FromHours(-5));
        var item = ReminderScheduleCalculator.Next(
            ReminderCategory.Activity, preference.ActivityTime, preference, zone, now);
        var local = TimeZoneInfo.ConvertTime(item.DueAtUtc, zone);
        Assert.Equal(3, local.Hour);
        Assert.Equal(0, local.Minute);
    }

    [Fact]
    public void AutumnFallBack_AmbiguousTimeUsesFirstOccurrence()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var preference = new ReminderPreference
        {
            OptedIn = true,
            ActivityTime = new TimeOnly(1, 30),
            QuietStart = new TimeOnly(6, 0), // equal start/end disables quiet mode for DST tests
            QuietEnd = new TimeOnly(6, 0),
        };
        var now = new DateTimeOffset(2026, 11, 1, 0, 30, 0, TimeSpan.FromHours(-4));
        var item = ReminderScheduleCalculator.Next(
            ReminderCategory.Activity, preference.ActivityTime, preference, zone, now);
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 5, 30, 0, TimeSpan.Zero), item.DueAtUtc);
    }

    [Fact]
    public void PreviewHonorsOptOut_AndWeeklySummaryMondayOnly()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        var pref = new ReminderPreference
        {
            OptedIn = false,
            WeeklySummaryEnabled = true,
            WeeklySummaryTime = new TimeOnly(9, 0),
        };
        var now = new DateTimeOffset(2026, 11, 5, 10, 0, 0, TimeSpan.FromHours(7));
        Assert.Empty(ReminderScheduleCalculator.Preview(pref, zone.Id, now));
        pref.OptedIn = true;
        pref.ActivityEnabled = false;
        pref.MealLoggingEnabled = false;
        var next = Assert.Single(ReminderScheduleCalculator.Preview(pref, zone.Id, now));
        var local = TimeZoneInfo.ConvertTime(next.DueAtUtc, zone);
        Assert.Equal(DayOfWeek.Monday, local.DayOfWeek);
        Assert.Equal(9, local.Hour);
    }
}
