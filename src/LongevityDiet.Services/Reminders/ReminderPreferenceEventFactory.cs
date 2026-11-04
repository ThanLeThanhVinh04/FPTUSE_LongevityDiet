using System.Text.Json;
using LongevityDiet.Domain.Entities;

namespace LongevityDiet.Services.Reminders;

/// <summary>
/// Single versioned payload contract for the Identity-owned outbox.
/// Profile and Reminder preference updates share the exact same schema.
/// </summary>
public static class ReminderPreferenceEventFactory
{
    public static IdentityOutboxEvent Create(
        Guid userId,
        string timeZone,
        bool activeAccount,
        ReminderPreference preference,
        DateTimeOffset occurredAtUtc)
    {
        var id = Guid.NewGuid();
        var payload = JsonSerializer.Serialize(new
        {
            eventId = id,
            userId,
            timeZone,
            enabled = preference.OptedIn,
            activeAccount,
            preference.MealLoggingEnabled,
            preference.ActivityEnabled,
            preference.ChallengeEnabled,
            preference.WeeklySummaryEnabled,
            preference.MealTime,
            preference.ActivityTime,
            preference.ChallengeTime,
            preference.WeeklySummaryTime,
            preference.QuietStart,
            preference.QuietEnd,
            preference.Version,
        });
        return new IdentityOutboxEvent
        {
            Id = id,
            UserId = userId,
            EventType = "ReminderPreferenceUpdated.v1",
            PayloadJson = payload,
            CreatedAtUtc = occurredAtUtc.UtcDateTime,
        };
    }
}
