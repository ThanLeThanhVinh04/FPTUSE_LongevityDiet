namespace LongevityDiet.Domain.Entities;

public enum ReminderCategory
{
    MealLogging = 1,
    Activity = 2,
    Challenge = 3,
    WeeklySummary = 4
}

// Identity & Profile owned data. Worker does not read this database.
public sealed class ReminderPreference
{
    public Guid UserId { get; set; }
    public bool OptedIn { get; set; }
    public bool MealLoggingEnabled { get; set; } = true;
    public bool ActivityEnabled { get; set; } = true;
    public bool ChallengeEnabled { get; set; }
    public bool WeeklySummaryEnabled { get; set; }
    public TimeOnly MealTime { get; set; } = new(20, 0);
    public TimeOnly ActivityTime { get; set; } = new(18, 30);
    public TimeOnly ChallengeTime { get; set; } = new(21, 0);
    public TimeOnly WeeklySummaryTime { get; set; } = new(9, 0);
    public TimeOnly QuietStart { get; set; } = new(22, 0);
    public TimeOnly QuietEnd { get; set; } = new(7, 0);
    public int Version { get; set; } = 1;
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class UserNotification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public ReminderCategory Category { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string DeepLink { get; set; } = "/app";
    public string DeduplicationKey { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReadAtUtc { get; set; }
}

public sealed class IdentityOutboxEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public int PublishAttempts { get; set; }
}

public sealed class IdentityInboxEvent
{
    public Guid EventId { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
}
