using System.Globalization;
using LongevityDiet.Domain.Entities;

namespace LongevityDiet.Domain.Recommendation;

public sealed record RecommendationCandidateInput(
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
    bool IsActive,
    bool HasSafetyGateViolation = false);

public sealed record RecommendationSoftWeightProfile(
    string Version,
    decimal BaseScore,
    decimal PlantFoundationWeight,
    decimal LegumeProteinWeight,
    decimal WholeGrainComplexCarbsWeight,
    decimal LowSugarSatFatWeight,
    decimal AgeProteinFitWeight,
    decimal ModerateFishWeight,
    decimal HighFiberWeight,
    decimal CuisineMatchWeight,
    decimal PreferredFoodBoostWeight,
    decimal DislikePenaltyWeight,
    decimal CurrentMealRepetitionPenaltyWeight)
{
    public static RecommendationSoftWeightProfile DefaultV2 { get; } = new(
        Version: "LDC-WEIGHTS-V2.0",
        BaseScore: 30m,
        PlantFoundationWeight: 25m,
        LegumeProteinWeight: 20m,
        WholeGrainComplexCarbsWeight: 15m,
        LowSugarSatFatWeight: 15m,
        AgeProteinFitWeight: 12m,
        ModerateFishWeight: 10m,
        HighFiberWeight: 6m,
        CuisineMatchWeight: 10m,
        PreferredFoodBoostWeight: 8m,
        DislikePenaltyWeight: -18m,
        CurrentMealRepetitionPenaltyWeight: -8m);
}

public sealed record RecommendationNormalizedFeatures(
    decimal PlantFoundationNorm,
    decimal LegumeProteinNorm,
    decimal WholeGrainComplexCarbsNorm,
    decimal LowSugarSatFatNorm,
    decimal AgeProteinFitNorm,
    decimal ModerateFishNorm,
    decimal HighFiberNorm,
    decimal CuisineMatchNorm,
    decimal PreferredFoodNorm,
    decimal DislikePenaltyNorm,
    decimal CurrentMealRepetitionNorm);

public sealed record RecommendationScoreComponent(
    string ComponentCode,
    string Label,
    decimal NormalizedValue,
    decimal Weight,
    decimal WeightedDelta,
    bool IsPenalty);

public sealed record RecommendationReasonDetail(
    string ReasonCode,
    string Title,
    string Explanation);

public sealed record RankedRecommendationCandidate(
    int Rank,
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
    decimal FinalScore,
    bool IsDisliked,
    bool IsPreferred,
    RecommendationNormalizedFeatures NormalizedFeatures,
    IReadOnlyList<RecommendationScoreComponent> ScoreComponents,
    IReadOnlyList<string> ReasonCodes,
    IReadOnlyList<RecommendationReasonDetail> ReasonDetails,
    string DeterministicExplanation,
    string ExplanationText,
    string ExplanationSource);

public sealed record RejectedRecommendationCandidate(
    string CandidateCode,
    string Title,
    IReadOnlyList<string> RejectionReasonCodes,
    string RejectionExplanation);

public sealed record RecommendationRankRequest(
    string ContractVersion,
    string RuleSetVersion,
    string WeightProfileVersion,
    MealSlotType TargetSlot,
    string DietaryPattern,
    int UserAgeYears,
    string? PreferredCuisine,
    IReadOnlyList<string> Allergies,
    IReadOnlyList<string> ExplicitExclusions,
    IReadOnlyList<string> DislikedTokens,
    IReadOnlyList<string> PreferredTokens,
    string? CurrentCandidateCode,
    IReadOnlyList<RecommendationCandidateInput> Candidates);

public sealed record RecommendationRankResult(
    bool Succeeded,
    string? ErrorCode,
    string? ErrorMessage,
    string ContractVersion,
    string RuleSetVersion,
    string WeightProfileVersion,
    MealSlotType TargetSlot,
    IReadOnlyList<RankedRecommendationCandidate> AcceptedCandidates,
    IReadOnlyList<RejectedRecommendationCandidate> RejectedCandidates);

public static class RecommendationRankingPipeline
{
    private static readonly HashSet<string> SupportedDietaryPatterns =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "PlantForward",
            "Pescatarian",
            "StrictlyPlantBased"
        };

    public static RecommendationRankResult Execute(RecommendationRankRequest request)
    {
        // Step 1: Validate request
        var validationError = ValidateRequest(request);
        if (validationError is not null)
        {
            return new RecommendationRankResult(
                Succeeded: false,
                ErrorCode: validationError.Value.Code,
                ErrorMessage: validationError.Value.Message,
                ContractVersion: request.ContractVersion,
                RuleSetVersion: request.RuleSetVersion,
                WeightProfileVersion: request.WeightProfileVersion,
                TargetSlot: request.TargetSlot,
                AcceptedCandidates: [],
                RejectedCandidates: []);
        }

        var weights = ResolveWeightProfile(request.WeightProfileVersion);
        var allergySet = NormalizeSet(request.Allergies);
        var exclusionSet = NormalizeSet(request.ExplicitExclusions);
        var dislikedSet = NormalizeSet(request.DislikedTokens);
        var preferredSet = NormalizeSet(request.PreferredTokens);
        var strictlyPlantBased = string.Equals(
            request.DietaryPattern,
            "StrictlyPlantBased",
            StringComparison.OrdinalIgnoreCase);

        var acceptedUnsorted = new List<RankedRecommendationCandidate>();
        var rejectedList = new List<RejectedRecommendationCandidate>();

        foreach (var candidate in request.Candidates)
        {
            // Step 2: Apply hard constraints
            var rejectionReasons = EvaluateHardConstraints(
                candidate,
                request.TargetSlot,
                strictlyPlantBased,
                allergySet,
                exclusionSet);

            if (rejectionReasons.Count > 0)
            {
                rejectedList.Add(new RejectedRecommendationCandidate(
                    candidate.CandidateCode,
                    candidate.Title,
                    rejectionReasons,
                    BuildRejectionExplanation(candidate.Title, rejectionReasons)));
                continue;
            }

            // Step 3: Normalize candidate features
            var isDisliked = MatchesTokenSet(candidate, dislikedSet);
            var isPreferred = MatchesTokenSet(candidate, preferredSet);
            var isCurrentMeal = !string.IsNullOrWhiteSpace(request.CurrentCandidateCode) &&
                string.Equals(
                    candidate.CandidateCode.Trim(),
                    request.CurrentCandidateCode.Trim(),
                    StringComparison.OrdinalIgnoreCase);

            var normalized = NormalizeFeatures(
                candidate,
                request.DietaryPattern,
                request.UserAgeYears,
                request.PreferredCuisine,
                isPreferred,
                isDisliked,
                isCurrentMeal);

            // Step 4 & Step 5: Apply versioned soft weights & calculate score components
            var (finalScore, components, reasonCodes, reasonDetails) = CalculateScoreAndReasons(
                candidate,
                normalized,
                weights,
                request.UserAgeYears);

            var deterministicExplanation = BuildDeterministicExplanation(
                candidate,
                reasonDetails,
                request.RuleSetVersion,
                weights.Version,
                isDisliked);

            acceptedUnsorted.Add(new RankedRecommendationCandidate(
                Rank: 0,
                CandidateCode: candidate.CandidateCode,
                Title: candidate.Title,
                Description: candidate.Description,
                Cuisine: candidate.Cuisine,
                ServingSize: candidate.ServingSize,
                ServingUnit: candidate.ServingUnit,
                CaloriesKcal: candidate.CaloriesKcal,
                ProteinGrams: candidate.ProteinGrams,
                CarbsGrams: candidate.CarbsGrams,
                FatGrams: candidate.FatGrams,
                FiberGrams: candidate.FiberGrams,
                IsPlantBased: candidate.IsPlantBased,
                HasLegumes: candidate.HasLegumes,
                HasWholeGrains: candidate.HasWholeGrains,
                ContainsFish: candidate.ContainsFish,
                Allergens: candidate.Allergens,
                Ingredients: candidate.Ingredients,
                FinalScore: finalScore,
                IsDisliked: isDisliked,
                IsPreferred: isPreferred,
                NormalizedFeatures: normalized,
                ScoreComponents: components,
                ReasonCodes: reasonCodes,
                ReasonDetails: reasonDetails,
                DeterministicExplanation: deterministicExplanation,
                ExplanationText: deterministicExplanation,
                ExplanationSource: "DETERMINISTIC_TEMPLATE"));
        }

        // Step 6: Stable sort / tie-break
        var acceptedRanked = acceptedUnsorted
            .OrderByDescending(x => x.FinalScore)
            .ThenBy(x => x.IsDisliked)
            .ThenBy(x => x.CandidateCode, StringComparer.Ordinal)
            .Select((candidate, index) => candidate with { Rank = index + 1 })
            .ToList();

        var rejectedSorted = rejectedList
            .OrderBy(x => x.CandidateCode, StringComparer.Ordinal)
            .ToList();

        // Step 7: Return accepted/rejected candidates + reason codes
        return new RecommendationRankResult(
            Succeeded: true,
            ErrorCode: null,
            ErrorMessage: null,
            ContractVersion: string.IsNullOrWhiteSpace(request.ContractVersion) ? "2026-04-v2" : request.ContractVersion,
            RuleSetVersion: request.RuleSetVersion,
            WeightProfileVersion: weights.Version,
            TargetSlot: request.TargetSlot,
            AcceptedCandidates: acceptedRanked,
            RejectedCandidates: rejectedSorted);
    }

    private static (string Code, string Message)? ValidateRequest(RecommendationRankRequest request)
    {
        if (request is null)
        {
            return ("invalid_request", "Recommendation request must not be null.");
        }

        if (string.IsNullOrWhiteSpace(request.RuleSetVersion))
        {
            return ("invalid_rule_set_version", "RuleSetVersion is required.");
        }

        if (string.IsNullOrWhiteSpace(request.DietaryPattern) ||
            !SupportedDietaryPatterns.Contains(request.DietaryPattern.Trim()))
        {
            return ("invalid_dietary_pattern", "DietaryPattern must be PlantForward, Pescatarian, or StrictlyPlantBased.");
        }

        if (request.UserAgeYears is < 18 or > 120)
        {
            return ("invalid_user_age", "UserAgeYears must be between 18 and 120.");
        }

        if (request.Candidates is null || request.Candidates.Count == 0)
        {
            return ("empty_candidates", "At least one candidate meal must be provided for ranking.");
        }

        return null;
    }

    public static RecommendationSoftWeightProfile ResolveWeightProfile(string? version) =>
        RecommendationSoftWeightProfile.DefaultV2;

    private static List<string> EvaluateHardConstraints(
        RecommendationCandidateInput candidate,
        MealSlotType targetSlot,
        bool strictlyPlantBased,
        HashSet<string> allergies,
        HashSet<string> explicitExclusions)
    {
        var reasons = new List<string>();

        if (!candidate.IsActive)
        {
            reasons.Add("REJECTED_INACTIVE_CANDIDATE");
        }

        if (candidate.HasSafetyGateViolation || candidate.CaloriesKcal <= 0 || candidate.ServingSize <= 0m)
        {
            reasons.Add("REJECTED_UNSAFE_STATE");
        }

        if (candidate.SupportedSlots is null || !candidate.SupportedSlots.Contains(targetSlot))
        {
            reasons.Add("REJECTED_UNSUPPORTED_SLOT");
        }

        if (strictlyPlantBased && (!candidate.IsPlantBased || candidate.ContainsFish))
        {
            reasons.Add("REJECTED_DIETARY_PATTERN_CONFLICT");
        }

        if (MatchesTokenSet(candidate, allergies))
        {
            reasons.Add("REJECTED_HARD_ALLERGEN");
        }

        if (MatchesTokenSet(candidate, explicitExclusions))
        {
            reasons.Add("REJECTED_EXPLICIT_EXCLUSION");
        }

        return reasons;
    }

    private static RecommendationNormalizedFeatures NormalizeFeatures(
        RecommendationCandidateInput candidate,
        string dietaryPattern,
        int userAgeYears,
        string? preferredCuisine,
        bool isPreferredFood,
        bool isDislikedFood,
        bool isCurrentMeal)
    {
        var plantNorm = candidate.IsPlantBased ? 1.0m : 0.35m;
        var legumeNorm = candidate.HasLegumes ? 1.0m : 0.0m;

        var wholeGrainBase = candidate.HasWholeGrains ? 0.75m : 0.0m;
        var fiberContribution = Math.Clamp(candidate.FiberGrams / 12.0m, 0.0m, 1.0m) * 0.25m;
        var wholeGrainNorm = decimal.Round(Math.Clamp(wholeGrainBase + fiberContribution, 0.0m, 1.0m), 4);

        var lowSugarSatFatNorm = 0.0m;
        if (candidate.SugarGrams <= 6.5m && candidate.SaturatedFatGrams <= 2.5m)
        {
            lowSugarSatFatNorm = 1.0m;
        }
        else if (candidate.SugarGrams <= 9.0m && candidate.SaturatedFatGrams <= 3.5m)
        {
            lowSugarSatFatNorm = 0.5m;
        }
        else if (candidate.SugarGrams > 12.0m || candidate.SaturatedFatGrams > 5.0m)
        {
            lowSugarSatFatNorm = -0.8m;
        }

        var ageProteinNorm = 0.0m;
        if (userAgeYears >= 65)
        {
            if (candidate.ProteinGrams >= 16.0m)
            {
                ageProteinNorm = 1.0m;
            }
            else if (candidate.ProteinGrams >= 12.0m)
            {
                ageProteinNorm = 0.5m;
            }
        }
        else
        {
            if (candidate.ProteinGrams is >= 7.0m and <= 24.0m)
            {
                ageProteinNorm = 1.0m;
            }
            else if (candidate.ProteinGrams is > 24.0m and <= 28.0m)
            {
                ageProteinNorm = 0.4m;
            }
        }

        var fishNorm = 0.0m;
        if (candidate.ContainsFish)
        {
            if (string.Equals(dietaryPattern, "Pescatarian", StringComparison.OrdinalIgnoreCase))
            {
                fishNorm = 1.8m;
            }
            else if (string.Equals(dietaryPattern, "PlantForward", StringComparison.OrdinalIgnoreCase))
            {
                fishNorm = 1.0m;
            }
        }

        var highFiberNorm = candidate.FiberGrams >= 8.0m
            ? 1.0m
            : candidate.FiberGrams >= 6.0m
                ? 0.5m
                : 0.0m;

        var cuisineNorm = !string.IsNullOrWhiteSpace(preferredCuisine) &&
            string.Equals(candidate.Cuisine, preferredCuisine.Trim(), StringComparison.OrdinalIgnoreCase)
                ? 1.0m
                : 0.0m;

        var preferredNorm = isPreferredFood ? 1.0m : 0.0m;
        var dislikeNorm = isDislikedFood ? 1.0m : 0.0m;
        var currentMealNorm = isCurrentMeal ? 1.0m : 0.0m;

        return new RecommendationNormalizedFeatures(
            PlantFoundationNorm: plantNorm,
            LegumeProteinNorm: legumeNorm,
            WholeGrainComplexCarbsNorm: wholeGrainNorm,
            LowSugarSatFatNorm: lowSugarSatFatNorm,
            AgeProteinFitNorm: ageProteinNorm,
            ModerateFishNorm: fishNorm,
            HighFiberNorm: highFiberNorm,
            CuisineMatchNorm: cuisineNorm,
            PreferredFoodNorm: preferredNorm,
            DislikePenaltyNorm: dislikeNorm,
            CurrentMealRepetitionNorm: currentMealNorm);
    }

    private static (
        decimal FinalScore,
        List<RecommendationScoreComponent> Components,
        List<string> ReasonCodes,
        List<RecommendationReasonDetail> ReasonDetails) CalculateScoreAndReasons(
        RecommendationCandidateInput candidate,
        RecommendationNormalizedFeatures features,
        RecommendationSoftWeightProfile weights,
        int userAgeYears)
    {
        var components = new List<RecommendationScoreComponent>
        {
            new(
                "BASE_SAFETY_VERIFIED",
                "Điểm nền món ăn đạt chuẩn an toàn",
                1.0m,
                weights.BaseScore,
                weights.BaseScore,
                IsPenalty: false)
        };

        var reasonCodes = new List<string>();
        var reasonDetails = new List<RecommendationReasonDetail>();
        var score = weights.BaseScore;

        if (features.PlantFoundationNorm >= 1.0m)
        {
            var delta = decimal.Round(features.PlantFoundationNorm * weights.PlantFoundationWeight, 2);
            score += delta;
            components.Add(new("PLANT_FORWARD", "Nền tảng thực vật chủ đạo", features.PlantFoundationNorm, weights.PlantFoundationWeight, delta, false));
            reasonCodes.Add("PLANT_FORWARD");
            reasonDetails.Add(new(
                "PLANT_FORWARD",
                "Nền tảng thực vật chủ đạo",
                "Món ăn ưu tiên rau củ, hạt và chất béo thực vật lành mạnh theo trụ cột Longevity Diet."));
        }

        if (features.LegumeProteinNorm > 0m)
        {
            var delta = decimal.Round(features.LegumeProteinNorm * weights.LegumeProteinWeight, 2);
            score += delta;
            components.Add(new("LEGUME_PROTEIN", "Đạm thực vật từ các loại đậu", features.LegumeProteinNorm, weights.LegumeProteinWeight, delta, false));
            reasonCodes.Add("LEGUME_PROTEIN");
            reasonDetails.Add(new(
                "LEGUME_PROTEIN",
                "Đạm thực vật từ các loại đậu",
                "Cung cấp đạm từ đậu lăng, đậu gà hoặc đậu đen giúp giảm phụ thuộc vào đạm động vật."));
        }

        if (candidate.HasWholeGrains)
        {
            var delta = decimal.Round(features.WholeGrainComplexCarbsNorm * weights.WholeGrainComplexCarbsWeight, 2);
            score += delta;
            components.Add(new("COMPLEX_CARBS", "Tinh bột phức hợp nguyên cám", features.WholeGrainComplexCarbsNorm, weights.WholeGrainComplexCarbsWeight, delta, false));
            reasonCodes.Add("COMPLEX_CARBS");
            reasonDetails.Add(new(
                "COMPLEX_CARBS",
                "Tinh bột phức hợp nguyên cám",
                "Sử dụng ngũ cốc nguyên hạt giải phóng năng lượng chậm và ổn định."));
        }

        if (features.LowSugarSatFatNorm != 0m)
        {
            var delta = decimal.Round(features.LowSugarSatFatNorm * weights.LowSugarSatFatWeight, 2);
            score += delta;
            components.Add(new(
                "LOW_SUGAR_SATFAT",
                "Kiểm soát đường và chất béo bão hòa",
                features.LowSugarSatFatNorm,
                weights.LowSugarSatFatWeight,
                delta,
                IsPenalty: delta < 0m));

            if (delta > 0m)
            {
                reasonCodes.Add("LOW_SUGAR_SATFAT");
                reasonDetails.Add(new(
                    "LOW_SUGAR_SATFAT",
                    "Ít đường tinh luyện & chất béo bão hòa",
                    string.Create(CultureInfo.InvariantCulture, $"Lượng đường ({candidate.SugarGrams:0.#}g) và chất béo bão hòa ({candidate.SaturatedFatGrams:0.#}g) ở mức thấp an toàn.")));
            }
        }

        if (features.ModerateFishNorm > 0m)
        {
            var delta = decimal.Round(features.ModerateFishNorm * weights.ModerateFishWeight, 2);
            score += delta;
            components.Add(new("MODERATE_FISH", "Bổ sung cá giàu Omega-3 hợp lý", features.ModerateFishNorm, weights.ModerateFishWeight, delta, false));
            reasonCodes.Add("MODERATE_FISH");
            reasonDetails.Add(new(
                "MODERATE_FISH",
                "Bổ sung cá giàu Omega-3",
                "Phù hợp khuyến nghị 2–3 bữa cá/tuần trong chế độ ăn trường thọ."));
        }

        if (features.AgeProteinFitNorm > 0m)
        {
            var delta = decimal.Round(features.AgeProteinFitNorm * weights.AgeProteinFitWeight, 2);
            score += delta;
            components.Add(new("AGE_PROTEIN_FIT", "Định lượng đạm phù hợp độ tuổi", features.AgeProteinFitNorm, weights.AgeProteinFitWeight, delta, false));
            reasonCodes.Add("AGE_PROTEIN_FIT");
            var ageExplain = userAgeYears >= 65
                ? string.Create(CultureInfo.InvariantCulture, $"Cung cấp {candidate.ProteinGrams:0.#}g đạm hỗ trợ duy trì khối cơ cho nhóm tuổi 65+.")
                : string.Create(CultureInfo.InvariantCulture, $"Cung cấp {candidate.ProteinGrams:0.#}g đạm vừa phải, phù hợp nhóm tuổi trưởng thành dưới 65.");
            reasonDetails.Add(new("AGE_PROTEIN_FIT", "Đạm phù hợp độ tuổi", ageExplain));
        }

        if (features.HighFiberNorm > 0m)
        {
            var delta = decimal.Round(features.HighFiberNorm * weights.HighFiberWeight, 2);
            score += delta;
            components.Add(new("HIGH_FIBER", "Giàu chất xơ tự nhiên", features.HighFiberNorm, weights.HighFiberWeight, delta, false));
            reasonCodes.Add("HIGH_FIBER");
            reasonDetails.Add(new(
                "HIGH_FIBER",
                "Giàu chất xơ tự nhiên",
                string.Create(CultureInfo.InvariantCulture, $"Đạt {candidate.FiberGrams:0.#}g chất xơ mỗi khẩu phần, hỗ trợ hệ vi sinh đường ruột.")));
        }

        if (features.CuisineMatchNorm > 0m)
        {
            var delta = decimal.Round(features.CuisineMatchNorm * weights.CuisineMatchWeight, 2);
            score += delta;
            components.Add(new("CUISINE_MATCH", "Phù hợp khẩu vị ẩm thực ưu tiên", features.CuisineMatchNorm, weights.CuisineMatchWeight, delta, false));
            reasonCodes.Add("CUISINE_MATCH");
            reasonDetails.Add(new(
                "CUISINE_MATCH",
                "Hợp khẩu vị truyền thống",
                string.Create(CultureInfo.InvariantCulture, $"Khớp với nhóm ẩm thực {candidate.Cuisine} mà bạn ưu tiên.")));
        }

        if (features.PreferredFoodNorm > 0m)
        {
            var delta = decimal.Round(features.PreferredFoodNorm * weights.PreferredFoodBoostWeight, 2);
            score += delta;
            components.Add(new("USER_PREFERRED_BOOST", "Ưu tiên theo phản hồi yêu thích", features.PreferredFoodNorm, weights.PreferredFoodBoostWeight, delta, false));
            reasonCodes.Add("USER_PREFERRED_BOOST");
            reasonDetails.Add(new(
                "USER_PREFERRED_BOOST",
                "Món/nguyên liệu bạn yêu thích",
                "Được cộng điểm ưu tiên mềm dựa trên đánh giá yêu thích hoặc cài đặt sở thích của bạn."));
        }

        if (features.DislikePenaltyNorm > 0m)
        {
            var delta = decimal.Round(features.DislikePenaltyNorm * weights.DislikePenaltyWeight, 2);
            score += delta;
            components.Add(new("SOFT_DISLIKE_PENALTY", "Giảm ưu tiên do phản hồi không thích", features.DislikePenaltyNorm, weights.DislikePenaltyWeight, delta, true));
            reasonCodes.Add("SOFT_DISLIKE_PENALTY");
            reasonDetails.Add(new(
                "SOFT_DISLIKE_PENALTY",
                "Đã giảm bậc ưu tiên (Không thích)",
                "Món này vẫn an toàn với dị ứng nhưng được xếp thấp hơn do bạn từng đánh dấu không thích (khác với loại trừ cứng)."));
        }

        if (features.CurrentMealRepetitionNorm > 0m)
        {
            var delta = decimal.Round(features.CurrentMealRepetitionNorm * weights.CurrentMealRepetitionPenaltyWeight, 2);
            score += delta;
            components.Add(new("VARIETY_ROTATION_DELTA", "Ưu tiên đổi món mới khác món hiện tại", features.CurrentMealRepetitionNorm, weights.CurrentMealRepetitionPenaltyWeight, delta, true));
        }

        var clampedScore = Math.Max(0m, decimal.Round(score, 2));
        return (clampedScore, components, reasonCodes, reasonDetails);
    }

    private static string BuildDeterministicExplanation(
        RecommendationCandidateInput candidate,
        List<RecommendationReasonDetail> reasonDetails,
        string ruleSetVersion,
        string weightProfileVersion,
        bool isDisliked)
    {
        var positiveTitles = reasonDetails
            .Where(x => !string.Equals(x.ReasonCode, "SOFT_DISLIKE_PENALTY", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Title.ToLowerInvariant())
            .Take(4)
            .ToList();

        var summaryReasons = positiveTitles.Count > 0
            ? string.Join(", ", positiveTitles)
            : "cân bằng dinh dưỡng hằng ngày";

        var dislikeNote = isDisliked
            ? " Lưu ý: món này bị trừ điểm ưu tiên mềm do bạn từng đánh dấu không thích."
            : string.Empty;

        return string.Create(
            CultureInfo.InvariantCulture,
            $"Gợi ý {candidate.Title} vì đáp ứng {summaryReasons} theo bộ quy tắc {ruleSetVersion} (trọng số {weightProfileVersion}, đã vượt qua toàn bộ kiểm tra dị ứng và loại trừ cứng).{dislikeNote}");
    }

    private static string BuildRejectionExplanation(string title, List<string> rejectionReasonCodes)
    {
        var labels = rejectionReasonCodes.Select(code => code switch
        {
            "REJECTED_HARD_ALLERGEN" => "chứa thành phần gây dị ứng (ràng buộc cứng)",
            "REJECTED_EXPLICIT_EXCLUSION" => "nằm trong danh sách thực phẩm loại trừ cứng của bạn",
            "REJECTED_DIETARY_PATTERN_CONFLICT" => "không phù hợp với chế độ ăn thuần thực vật (StrictlyPlantBased)",
            "REJECTED_UNSUPPORTED_SLOT" => "không thuộc khung bữa ăn đang chọn",
            "REJECTED_INACTIVE_CANDIDATE" => "món ăn hiện ngừng hoạt động trong danh mục",
            "REJECTED_UNSAFE_STATE" => "dữ liệu dinh dưỡng hoặc trạng thái an toàn không hợp lệ",
            _ => code
        });

        return string.Create(
            CultureInfo.InvariantCulture,
            $"Đã loại {title} khỏi danh sách gợi ý vì: {string.Join("; ", labels)}.");
    }

    private static bool MatchesTokenSet(
        RecommendationCandidateInput candidate,
        HashSet<string> normalizedSet)
    {
        if (normalizedSet.Count == 0)
        {
            return false;
        }

        var normalizedCode = NormalizeToken(candidate.CandidateCode);
        if (normalizedSet.Contains(normalizedCode))
        {
            return true;
        }

        foreach (var allergen in candidate.Allergens)
        {
            var token = NormalizeToken(allergen);
            if (normalizedSet.Contains(token))
            {
                return true;
            }
        }

        foreach (var ingredient in candidate.Ingredients)
        {
            var token = NormalizeToken(ingredient);
            if (normalizedSet.Contains(token))
            {
                return true;
            }

            if (normalizedSet.Any(entry => entry.Length >= 3 && token.Contains(entry, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        var normalizedTitle = NormalizeToken(candidate.Title);
        foreach (var entry in normalizedSet)
        {
            if (entry.Length >= 3 &&
                (normalizedCode.Contains(entry, StringComparison.OrdinalIgnoreCase) ||
                 normalizedTitle.Contains(entry, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    public static HashSet<string> NormalizeSet(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in values)
        {
            if (string.IsNullOrWhiteSpace(item))
            {
                continue;
            }

            foreach (var part in item.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var normalized = NormalizeToken(part);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    result.Add(normalized);
                }
            }
        }

        return result;
    }

    public static string NormalizeToken(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        return raw.Trim().ToLowerInvariant();
    }
}

