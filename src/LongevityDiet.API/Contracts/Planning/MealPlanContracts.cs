using System.ComponentModel.DataAnnotations;

namespace LongevityDiet.API.Contracts.Planning;

public sealed record UpdateDietaryPreferenceRequest(
    [Required, MaxLength(64)] string DietaryPattern,
    IReadOnlyList<string>? Allergies,
    IReadOnlyList<string>? ExcludedFoods,
    [MaxLength(120)] string? PreferredCuisine);

public sealed record DietaryPreferenceResponse(
    Guid UserId,
    string DietaryPattern,
    IReadOnlyList<string> Allergies,
    IReadOnlyList<string> ExcludedFoods,
    string? PreferredCuisine,
    DateTimeOffset UpdatedAt);

public sealed record GenerateMealPlanRequest(
    DateOnly? StartDate,
    [Range(7, 14)] int Days = 7,
    [MaxLength(64)] string? DietaryPattern = null,
    IReadOnlyList<string>? Allergies = null,
    IReadOnlyList<string>? ExcludedFoods = null,
    [MaxLength(120)] string? PreferredCuisine = null);

public sealed record RegenerateMealPlanRequest(
    DateOnly? FromDate = null);

public sealed record RegeneratePlannedMealRequest(
    IReadOnlyList<string>? AdditionalExcludedCodes = null);

public sealed record CandidateMealResponse(
    string CandidateCode,
    string Title,
    string Description,
    string Category,
    string Cuisine,
    IReadOnlyList<string> SupportedSlots,
    decimal ServingSize,
    string ServingUnit,
    int CaloriesKcal,
    decimal ProteinGrams,
    decimal CarbsGrams,
    decimal FatGrams,
    decimal FiberGrams,
    bool IsPlantBased,
    bool HasLegumes,
    bool HasWholeGrains,
    bool ContainsFish,
    IReadOnlyList<string> Allergens,
    IReadOnlyList<string> Ingredients);

public sealed record PlannedMealResponse(
    Guid Id,
    Guid MealPlanDayId,
    int SlotOrder,
    string SlotType,
    TimeOnly PlannedTime,
    string CandidateCode,
    string Title,
    string Description,
    string Cuisine,
    decimal ServingSize,
    string ServingUnit,
    int CaloriesKcal,
    decimal ProteinGrams,
    decimal CarbsGrams,
    decimal FatGrams,
    decimal FiberGrams,
    bool IsPlantBased,
    bool HasLegumes,
    bool HasWholeGrains,
    bool ContainsFish,
    IReadOnlyList<string> Allergens,
    IReadOnlyList<string> Ingredients,
    decimal DeterministicScore,
    IReadOnlyList<string> ReasonCodes,
    string ExplanationSummary,
    int RegenerationCount);

public sealed record MealPlanDayResponse(
    Guid Id,
    int DayNumber,
    DateOnly PlanDate,
    int TargetCaloriesKcal,
    int TotalPlannedCaloriesKcal,
    string DailyFocusCode,
    string DailyFocusTitle,
    IReadOnlyList<PlannedMealResponse> Meals);

public sealed record MealPlanResponse(
    Guid Id,
    Guid UserId,
    string RuleSetVersion,
    int RevisionNumber,
    Guid? PreviousPlanId,
    string Status,
    DateOnly StartDate,
    DateOnly EndDate,
    int MealsPerDay,
    string TimeZone,
    string DietaryPattern,
    IReadOnlyList<string> AppliedAllergies,
    IReadOnlyList<string> AppliedExclusions,
    IReadOnlyList<string> ReasonCodes,
    string PlanExplanation,
    DateTimeOffset GeneratedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<MealPlanDayResponse> Days);

