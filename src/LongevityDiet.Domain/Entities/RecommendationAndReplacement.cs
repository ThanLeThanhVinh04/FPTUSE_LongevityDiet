namespace LongevityDiet.Domain.Entities;

public sealed class RecommendationFeedback
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string CandidateCode { get; set; } = string.Empty;
    public string CandidateTitle { get; set; } = string.Empty;
    public Guid? PlannedMealId { get; set; }
    public RecommendationFeedbackType FeedbackType { get; set; } = RecommendationFeedbackType.Like;
    public string ReasonCode { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string RuleSetVersion { get; set; } = "LDC-RULES-2026.1";
    public string WeightProfileVersion { get; set; } = "LDC-WEIGHTS-V2.0";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum RecommendationFeedbackType
{
    Like = 1,
    Dislike = 2
}

public sealed class MealReplacementHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid MealPlanId { get; set; }
    public Guid MealPlanDayId { get; set; }
    public Guid PlannedMealId { get; set; }
    public int DayNumber { get; set; }
    public MealSlotType SlotType { get; set; } = MealSlotType.Breakfast;
    public string PreviousCandidateCode { get; set; } = string.Empty;
    public string PreviousTitle { get; set; } = string.Empty;
    public decimal PreviousScore { get; set; }
    public string ReplacementCandidateCode { get; set; } = string.Empty;
    public string ReplacementTitle { get; set; } = string.Empty;
    public decimal ReplacementScore { get; set; }
    public string ReplacementReasonCode { get; set; } = string.Empty;
    public string ReasonSummary { get; set; } = string.Empty;
    public string AppliedAllergiesCsv { get; set; } = string.Empty;
    public string AppliedExclusionsCsv { get; set; } = string.Empty;
    public string RuleSetVersion { get; set; } = "LDC-RULES-2026.1";
    public string WeightProfileVersion { get; set; } = "LDC-WEIGHTS-V2.0";
    public DateTimeOffset ReplacedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class UserFoodPreference
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string FoodToken { get; set; } = string.Empty;
    public string DisplayLabel { get; set; } = string.Empty;
    public FoodPreferenceKind PreferenceKind { get; set; } = FoodPreferenceKind.Preferred;
    public string Source { get; set; } = "UserSetting";
    public string? Notes { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum FoodPreferenceKind
{
    Preferred = 1,
    Disliked = 2,
    ExplicitExclusion = 3
}

