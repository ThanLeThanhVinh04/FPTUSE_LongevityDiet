using LongevityDiet.Domain.Entities;

namespace LongevityDiet.Services.Planning;

public sealed record PlanningUserContextSnapshot(
    string ContractVersion,
    Guid UserId,
    int? BirthYear,
    string TimeZone,
    TimeOnly? WakeTime,
    TimeOnly? SleepTime,
    int PreferredMealFrequency,
    string? FoodPreference,
    string DietaryPattern,
    IReadOnlyList<string> Allergies,
    IReadOnlyList<string> ExcludedFoods,
    string? PreferredCuisine);

public sealed record PlanningDietRuleSnapshot(
    string RuleKey,
    string Title,
    string SourceType,
    string SourceUrl,
    string EvidenceLevel,
    decimal BaseWeight);

public sealed record PlanningRuleSetSnapshot(
    string ContractVersion,
    string RuleSetVersion,
    DateTimeOffset PublishedAt,
    IReadOnlyList<PlanningDietRuleSnapshot> Rules);

public sealed record PlanningCandidateMealSnapshot(
    string ContractVersion,
    string CandidateCode,
    string Title,
    string Description,
    string Category,
    string Cuisine,
    IReadOnlyList<MealSlotType> SupportedSlots,
    decimal ServingSize,
    string ServingUnit,
    int CaloriesKcal,
    decimal ProteinGrams,
    decimal CarbsGrams,
    decimal FatGrams,
    decimal SaturatedFatGrams,
    decimal SugarGrams,
    decimal FiberGrams,
    bool IsPlantBased,
    bool HasLegumes,
    bool HasWholeGrains,
    bool ContainsFish,
    IReadOnlyList<string> Allergens,
    IReadOnlyList<string> Ingredients,
    bool IsActive);

public sealed record UpdateDietaryPreferenceCommand(
    string DietaryPattern,
    IReadOnlyList<string> Allergies,
    IReadOnlyList<string> ExcludedFoods,
    string? PreferredCuisine);

public sealed record DietaryPreferenceSummary(
    Guid UserId,
    string DietaryPattern,
    IReadOnlyList<string> Allergies,
    IReadOnlyList<string> ExcludedFoods,
    string? PreferredCuisine,
    DateTimeOffset UpdatedAt);

public sealed record GenerateMealPlanCommand(
    DateOnly? StartDate,
    int Days = 7,
    string? DietaryPattern = null,
    IReadOnlyList<string>? Allergies = null,
    IReadOnlyList<string>? ExcludedFoods = null,
    string? PreferredCuisine = null);

public sealed record RegenerateMealPlanCommand(
    DateOnly? FromDate = null);

public sealed record RegeneratePlannedMealCommand(
    IReadOnlyList<string>? AdditionalExcludedCodes = null);

public sealed record PlannedMealSummary(
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

public sealed record MealPlanDaySummary(
    Guid Id,
    int DayNumber,
    DateOnly PlanDate,
    int TargetCaloriesKcal,
    int TotalPlannedCaloriesKcal,
    string DailyFocusCode,
    string DailyFocusTitle,
    IReadOnlyList<PlannedMealSummary> Meals);

public sealed record MealPlanSummary(
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
    IReadOnlyList<MealPlanDaySummary> Days);

