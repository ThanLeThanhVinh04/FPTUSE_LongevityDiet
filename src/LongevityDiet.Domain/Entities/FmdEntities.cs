namespace LongevityDiet.Domain.Entities;

public enum FmdSafetyStatus
{
    EducationNotViewed = 1,
    EducationViewed = 2,
    AssessmentRequired = 3,
    AssessmentSubmitted = 4,
    EligibleForTracking = 5,
    ProfessionalReviewRequired = 6
}

public enum FmdCycleStatus
{
    Active = 1,
    Completed = 2,
    StoppedByUser = 3
}

public sealed class FmdEducationAcknowledgement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string EducationVersion { get; set; } = "FMD-EDU-2026.1";
    public DateTimeOffset AcknowledgedAt { get; set; } = DateTimeOffset.UtcNow;
    public string SourceCitation { get; set; } = "Valter Longo Foundation - Fasting Mimicking Diet Research & Safety Guidelines";
    public bool DisclaimerAccepted { get; set; } = true;
}

public sealed class FmdSafetyAssessment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string AssessmentVersion { get; set; } = "FMD-SAFETY-2026.1";
    public DateTimeOffset EvaluatedAt { get; set; } = DateTimeOffset.UtcNow;
    public FmdSafetyStatus ResultStatus { get; set; } = FmdSafetyStatus.AssessmentRequired;
    public bool HasHighRiskFlag { get; set; }
    public string TriggeredRiskCodesCsv { get; set; } = string.Empty;
    public string RecommendationsSummary { get; set; } = string.Empty;
    public string AnswersJson { get; set; } = "{}";
    public string? Notes { get; set; }
}

public sealed class FmdCycle
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public int CycleNumber { get; set; } = 1;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int TargetDays { get; set; } = 5;
    public FmdCycleStatus Status { get; set; } = FmdCycleStatus.Active;
    public string? Notes { get; set; }
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? StoppedAt { get; set; }
    public string? StopReason { get; set; }

    public ICollection<FmdCycleDay> Days { get; set; } = new List<FmdCycleDay>();
}

public sealed class FmdCycleDay
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FmdCycleId { get; set; }
    public int DayNumber { get; set; }
    public DateOnly PlanDate { get; set; }
    public int TargetCaloriesKcal { get; set; }
    public int? EnergyLevel { get; set; }
    public int? HungerLevel { get; set; }
    public decimal? WaterIntakeLiters { get; set; }
    public string? Symptoms { get; set; }
    public string? DailyNotes { get; set; }
    public bool IsCompleted { get; set; }
    public DateTimeOffset? LoggedAt { get; set; }

    public FmdCycle FmdCycle { get; set; } = null!;
}

