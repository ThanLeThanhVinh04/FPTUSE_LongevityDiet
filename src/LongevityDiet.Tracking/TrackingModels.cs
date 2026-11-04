namespace LongevityDiet.Tracking;

public enum ActivityKind
{
    Walking = 1,
    Moderate = 2,
    Vigorous = 3,
    Strength = 4
}

public sealed class ActivityLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public ActivityKind Kind { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public int DurationMinutes { get; set; }
    public string? Note { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class ActivityGoal
{
    public Guid UserId { get; set; }
    public int WeeklyMinutesTarget { get; set; } = 150;
    public int WeeklyStrengthSessionsTarget { get; set; } = 2;
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class ScoreConfigurationVersion
{
    public string Version { get; set; } = "ldas-v1";
    public bool Published { get; set; }
    public DateTimeOffset PublishedAtUtc { get; set; }
    public decimal ActivityWeight { get; set; }
    public decimal MealWeight { get; set; }
    public decimal EatingWindowWeight { get; set; }
    public string? SourceReference { get; set; }
}

public sealed class AdherenceScore
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public DateOnly LocalDate { get; set; }
    public string Version { get; set; } = "ldas-v1";
    public decimal? Score { get; set; }
    public decimal CoveragePercent { get; set; }
    public bool IsPartial { get; set; }
    public string MissingDataJson { get; set; } = "[]";
    public DateTimeOffset CalculatedAtUtc { get; set; }
    public List<ScoreDimension> Dimensions { get; set; } = [];
}

public sealed class ScoreDimension
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AdherenceScoreId { get; set; }
    public string Code { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal? RawScore { get; set; }
    public decimal? WeightedScore { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public AdherenceScore AdherenceScore { get; set; } = null!;
}

// Copied read model only. A Meal/Window producer must publish evidence
// before these signals may be treated as measured data.
public sealed class TrackingScoreSignal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public DateOnly LocalDate { get; set; }
    public string DimensionCode { get; set; } = string.Empty;
    public decimal Percent { get; set; }
    public Guid SourceEventId { get; set; }
    public string SourceVersion { get; set; } = string.Empty;
    public DateTimeOffset ReceivedAtUtc { get; set; }
}
