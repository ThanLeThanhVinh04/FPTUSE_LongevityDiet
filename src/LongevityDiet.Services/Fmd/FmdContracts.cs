using LongevityDiet.Domain.Entities;

namespace LongevityDiet.Services.Fmd;

public sealed record FmdEducationCitation(
    string Title,
    string SourceType,
    string Url,
    string EvidenceSummary);

public sealed record FmdEducationContent(
    string Version,
    string Title,
    string Summary,
    string ScopeDisclaimer,
    IReadOnlyList<string> KeyPrinciples,
    IReadOnlyList<string> AbsoluteContraindications,
    IReadOnlyList<FmdEducationCitation> Citations);

public sealed record FmdSafetyQuestionDto(
    string QuestionId,
    string Category,
    string QuestionText,
    string MedicalRationale,
    bool IsHighRiskIfTrue);

public sealed record FmdSafetyStatusSummary(
    FmdSafetyStatus CurrentStatus,
    string StatusTitle,
    string StatusMessage,
    bool IsEligibleForTracking,
    bool RequiresProfessionalReview,
    DateTimeOffset? LastAcknowledgedAt,
    DateTimeOffset? LastEvaluatedAt,
    IReadOnlyList<string> TriggeredRiskCodes,
    string? Notes);

public sealed record SubmitFmdAssessmentCommand(
    IReadOnlyDictionary<string, bool> QuestionAnswers,
    string? Notes = null);

public sealed record StartFmdCycleCommand(
    DateOnly? StartDate = null,
    string? Notes = null);

public sealed record LogFmdCycleDayCommand(
    int DayNumber,
    int? EnergyLevel,
    int? HungerLevel,
    decimal? WaterIntakeLiters,
    string? Symptoms,
    string? DailyNotes);

public sealed record StopFmdCycleCommand(
    string StopReason);

public sealed record FmdCycleDaySummary(
    Guid Id,
    int DayNumber,
    DateOnly PlanDate,
    int TargetCaloriesKcal,
    int? EnergyLevel,
    int? HungerLevel,
    decimal? WaterIntakeLiters,
    string? Symptoms,
    string? DailyNotes,
    bool IsCompleted,
    DateTimeOffset? LoggedAt);

public sealed record FmdCycleSummary(
    Guid Id,
    Guid UserId,
    int CycleNumber,
    DateOnly StartDate,
    DateOnly EndDate,
    int TargetDays,
    string Status,
    string? Notes,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? StoppedAt,
    string? StopReason,
    IReadOnlyList<FmdCycleDaySummary> Days);

