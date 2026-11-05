namespace LongevityDiet.Services.Tracking;

public sealed record ChallengeTemplateSummary(
    Guid Id,
    string Code,
    string Title,
    string Description,
    int DurationDays,
    IReadOnlyList<ChallengeDayTemplateSummary> Days);

public sealed record ChallengeDayTemplateSummary(int DayNumber, string Title, string Guidance);

public sealed record ChallengeDaySummary(
    Guid Id,
    int DayNumber,
    DateOnly LocalDate,
    string Title,
    string Guidance,
    bool IsCompleted,
    DateTimeOffset? CompletedAt);

public sealed record UserChallengeSummary(
    Guid Id,
    Guid ChallengeTemplateId,
    string TemplateCode,
    string Title,
    string TimeZone,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status,
    int CompletedDays,
    int CurrentStreak,
    int ProgressPercent,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<ChallengeDaySummary> Days);

public sealed record DashboardDayPoint(
    DateOnly LocalDate,
    int LoggedMealCount,
    int TotalCaloriesKcal,
    bool? EatingWindowWithinTarget,
    bool ChallengeDayCompleted,
    int ChallengeCurrentStreak,
    decimal LoggingConsistencyScore);

public sealed record DashboardSummary(
    DateOnly FromDate,
    DateOnly ToDate,
    string TimeZone,
    int LoggedDays,
    decimal LoggingConsistencyPercent,
    int? LatestCaloriesKcal,
    bool? LatestEatingWindowWithinTarget,
    int ChallengeCompletedDays,
    int ChallengeCurrentStreak,
    string ActivityAvailability,
    string LdasAvailability,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<DashboardDayPoint> Trend);

public sealed record ProgressProjectionInput(
    Guid EventId,
    string EventType,
    Guid UserId,
    DateOnly LocalDate);
