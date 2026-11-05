namespace LongevityDiet.Services.Tracking;

public sealed record WeeklyReportMetricSummary(string MetricKey, decimal? NumericValue, string? TextValue, string Unit);
public sealed record WeeklyReportSummary(
    Guid Id, Guid UserId, DateOnly WeekStart, DateOnly WeekEnd, int ReportVersion,
    string Status, string TimeZone, string Summary, DateTimeOffset GeneratedAt,
    IReadOnlyList<WeeklyReportMetricSummary> Metrics);

public sealed record WorkerJobSummary(
    Guid Id, string JobType, string IdempotencyKey, Guid? UserId, DateOnly? PeriodStart,
    int AttemptCount, int MaxAttempts, string Status, DateTimeOffset NextAttemptAt,
    DateTimeOffset UpdatedAt, string? LastError);

public sealed record WorkerDeadLetterSummary(
    Guid Id, Guid WorkerJobId, string JobType, string IdempotencyKey, string Error,
    int AttemptCount, DateTimeOffset DeadLetteredAt, DateTimeOffset? RecoveredAt);

public sealed record WorkerDiagnosticsSummary(
    DateTimeOffset? LastHeartbeatAt, string? InstanceId, IReadOnlyList<WorkerJobSummary> Jobs,
    IReadOnlyList<WorkerDeadLetterSummary> DeadLetters);
