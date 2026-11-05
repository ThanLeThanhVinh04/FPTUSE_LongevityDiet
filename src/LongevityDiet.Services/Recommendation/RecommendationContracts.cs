using LongevityDiet.Domain.Recommendation;
using LongevityDiet.Services.Planning;

namespace LongevityDiet.Services.Recommendation;

public interface IRecommendationEngineClient
{
    Task<RecommendationRankResult> RankMealsAsync(
        RecommendationRankRequest request,
        CancellationToken cancellationToken = default);
}

public interface IRecommendationExplanationTextGenerator
{
    Task<string?> TryRewriteExplanationAsync(
        RankedRecommendationCandidate candidate,
        string dietaryPattern,
        CancellationToken cancellationToken = default);
}

public sealed record GetMealRecommendationsQuery(
    string? SlotType = null,
    Guid? MealPlanId = null,
    Guid? PlannedMealId = null,
    IReadOnlyList<string>? AdditionalExcludedCodes = null);

public sealed record SubmitRecommendationFeedbackCommand(
    string CandidateCode,
    string FeedbackType,
    string? ReasonCode = null,
    string? Notes = null,
    Guid? PlannedMealId = null);

public sealed record UpsertFoodPreferenceCommand(
    string FoodToken,
    string PreferenceKind,
    string? DisplayLabel = null,
    string? Notes = null);

public sealed record ReplacePlannedMealCommand(
    Guid MealPlanId,
    Guid PlannedMealId,
    string? TargetCandidateCode = null,
    string? ReplacementReasonCode = null,
    string? ReasonNotes = null,
    IReadOnlyList<string>? AdditionalExcludedCodes = null);

public sealed record RecommendationCatalogResultSummary(
    string ContractVersion,
    string RuleSetVersion,
    string WeightProfileVersion,
    string TargetSlot,
    string DietaryPattern,
    IReadOnlyList<string> AppliedAllergies,
    IReadOnlyList<string> AppliedExclusions,
    IReadOnlyList<string> DislikedTokens,
    IReadOnlyList<string> PreferredTokens,
    IReadOnlyList<RankedRecommendationCandidate> AcceptedCandidates,
    IReadOnlyList<RejectedRecommendationCandidate> RejectedCandidates);

public sealed record RecommendationFeedbackSummary(
    Guid Id,
    Guid UserId,
    string CandidateCode,
    string CandidateTitle,
    Guid? PlannedMealId,
    string FeedbackType,
    string ReasonCode,
    string? Notes,
    string RuleSetVersion,
    string WeightProfileVersion,
    DateTimeOffset CreatedAt);

public sealed record UserFoodPreferenceSummary(
    Guid Id,
    Guid UserId,
    string FoodToken,
    string DisplayLabel,
    string PreferenceKind,
    string Source,
    string? Notes,
    DateTimeOffset UpdatedAt);

public sealed record MealReplacementHistorySummary(
    Guid Id,
    Guid UserId,
    Guid MealPlanId,
    Guid MealPlanDayId,
    Guid PlannedMealId,
    int DayNumber,
    string SlotType,
    string PreviousCandidateCode,
    string PreviousTitle,
    decimal PreviousScore,
    string ReplacementCandidateCode,
    string ReplacementTitle,
    decimal ReplacementScore,
    string ReplacementReasonCode,
    string ReasonSummary,
    IReadOnlyList<string> AppliedAllergies,
    IReadOnlyList<string> AppliedExclusions,
    string RuleSetVersion,
    string WeightProfileVersion,
    DateTimeOffset ReplacedAt);

public sealed record ReplacePlannedMealResultSummary(
    MealPlanSummary UpdatedPlan,
    PlannedMealSummary ReplacedMeal,
    MealReplacementHistorySummary ReplacementAudit);

