namespace LongevityDiet.Domain.Entities;

public enum WeeklyReportStatus { Pending = 1, Ready = 2, Failed = 3 }
public enum WorkerJobStatus { Pending = 1, Processing = 2, Completed = 3, DeadLetter = 4 }

public sealed class WeeklyReport
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateOnly WeekStart { get; set; }
    public DateOnly WeekEnd { get; set; }
    public int ReportVersion { get; set; } = 1;
    public WeeklyReportStatus Status { get; set; } = WeeklyReportStatus.Ready;
    public string TimeZone { get; set; } = "Asia/Ho_Chi_Minh";
    public string Summary { get; set; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; set; }
    public ICollection<WeeklyReportMetric> Metrics { get; set; } = [];
}

public sealed class WeeklyReportMetric
{
    public Guid Id { get; set; }
    public Guid WeeklyReportId { get; set; }
    public string MetricKey { get; set; } = string.Empty;
    public decimal? NumericValue { get; set; }
    public string? TextValue { get; set; }
    public string Unit { get; set; } = string.Empty;
    public WeeklyReport WeeklyReport { get; set; } = null!;
}

public sealed class WorkerJob
{
    public Guid Id { get; set; }
    public string JobType { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public DateOnly? PeriodStart { get; set; }
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; } = 5;
    public WorkerJobStatus Status { get; set; } = WorkerJobStatus.Pending;
    public DateTimeOffset NextAttemptAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? LastError { get; set; }
    public string? LockedBy { get; set; }
    public DateTimeOffset? LockedAt { get; set; }
}

public sealed class WorkerProcessedEvent
{
    public Guid EventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTimeOffset ProcessedAt { get; set; }
}

public sealed class WorkerDeadLetter
{
    public Guid Id { get; set; }
    public Guid WorkerJobId { get; set; }
    public string JobType { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
    public DateTimeOffset DeadLetteredAt { get; set; }
    public DateTimeOffset? RecoveredAt { get; set; }
}

public sealed class WorkerHeartbeat
{
    public string WorkerName { get; set; } = string.Empty;
    public DateTimeOffset LastSeenAt { get; set; }
    public string InstanceId { get; set; } = string.Empty;
}
