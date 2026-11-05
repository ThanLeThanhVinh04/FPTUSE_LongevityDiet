namespace LongevityDiet.API.Contracts.Recommendation;

public sealed record SubmitRecommendationFeedbackRequest(
    string CandidateCode,
    string FeedbackType,
    string? ReasonCode = null,
    string? Notes = null,
    Guid? PlannedMealId = null);

public sealed record UpsertFoodPreferenceRequest(
    string FoodToken,
    string PreferenceKind,
    string? DisplayLabel = null,
    string? Notes = null);

public sealed record ReplacePlannedMealRequest(
    Guid MealPlanId,
    Guid PlannedMealId,
    string? TargetCandidateCode = null,
    string? ReplacementReasonCode = null,
    string? ReasonNotes = null,
    IReadOnlyList<string>? AdditionalExcludedCodes = null);

public sealed record ReplacePlanMealByRouteRequest(
    string? TargetCandidateCode = null,
    string? ReplacementReasonCode = null,
    string? ReasonNotes = null,
    IReadOnlyList<string>? AdditionalExcludedCodes = null);

