namespace LongevityDiet.Domain.Entities;

public enum UserChallengeStatus
{
    Active = 1,
    Completed = 2,
    Abandoned = 3,
}

public sealed class ChallengeTemplate
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int DurationDays { get; set; } = 14;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public ICollection<ChallengeDayTemplate> Days { get; set; } = [];
}

public sealed class ChallengeDayTemplate
{
    public Guid Id { get; set; }
    public Guid ChallengeTemplateId { get; set; }
    public int DayNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Guidance { get; set; } = string.Empty;
    public ChallengeTemplate ChallengeTemplate { get; set; } = null!;
}

public sealed class UserChallenge
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ChallengeTemplateId { get; set; }
    public string TimeZone { get; set; } = "Asia/Ho_Chi_Minh";
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public UserChallengeStatus Status { get; set; } = UserChallengeStatus.Active;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public ChallengeTemplate ChallengeTemplate { get; set; } = null!;
    public ICollection<UserChallengeDay> Days { get; set; } = [];
}

public sealed class UserChallengeDay
{
    public Guid Id { get; set; }
    public Guid UserChallengeId { get; set; }
    public int DayNumber { get; set; }
    public DateOnly LocalDate { get; set; }
    public bool IsCompleted { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public UserChallenge UserChallenge { get; set; } = null!;
}

public sealed class DailyWellnessSummary
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateOnly LocalDate { get; set; }
    public int LoggedMealCount { get; set; }
    public int TotalCaloriesKcal { get; set; }
    public bool? EatingWindowWithinTarget { get; set; }
    public bool ChallengeDayCompleted { get; set; }
    public int ChallengeCurrentStreak { get; set; }
    public decimal LoggingConsistencyScore { get; set; }
    public string DataAvailability { get; set; } = "tracking,challenge";
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class ProgressEventReceipt
{
    public Guid EventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTimeOffset ProcessedAt { get; set; }
}
