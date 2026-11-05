using System.ComponentModel.DataAnnotations;

namespace LongevityDiet.API.Contracts.Fmd;

public sealed record SubmitFmdAssessmentRequest(
    [Required] IReadOnlyDictionary<string, bool> Answers,
    [MaxLength(600)] string? Notes = null);

public sealed record StartFmdCycleRequest(
    DateOnly? StartDate = null,
    [MaxLength(1000)] string? Notes = null);

public sealed record LogFmdCycleDayRequest(
    [Range(1, 5)] int DayNumber,
    [Range(1, 5)] int? EnergyLevel = null,
    [Range(1, 5)] int? HungerLevel = null,
    [Range(0, 10)] decimal? WaterIntakeLiters = null,
    [MaxLength(500)] string? Symptoms = null,
    [MaxLength(1000)] string? DailyNotes = null);

public sealed record StopFmdCycleRequest(
    [Required, MaxLength(500)] string StopReason);

