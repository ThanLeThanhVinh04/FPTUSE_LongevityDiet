using LongevityDiet.Domain.Entities;
using LongevityDiet.Domain.Recommendation;
using LongevityDiet.Services.Recommendation;

namespace LongevityDiet.UnitTests;

public sealed class RecommendationV2AndReplacementTests
{
    private static List<RecommendationCandidateInput> CreateSampleCandidates() =>
    [
        new(
            CandidateCode: "ldc-ln-chickpea-quinoa-bowl",
            Title: "Tô hạt diêm mạch, đậu gà nướng và bơ",
            Description: "Quinoa, đậu gà, cải xoăn và dầu ô-liu.",
            Category: "Bowl",
            Cuisine: "Mediterranean",
            SupportedSlots: [MealSlotType.Lunch, MealSlotType.Dinner],
            ServingSize: 1m,
            ServingUnit: "tô",
            CaloriesKcal: 480,
            ProteinGrams: 16.5m,
            CarbsGrams: 54m,
            FatGrams: 19m,
            SaturatedFatGrams: 2.3m,
            SugarGrams: 4.5m,
            FiberGrams: 11.5m,
            IsPlantBased: true,
            HasLegumes: true,
            HasWholeGrains: true,
            ContainsFish: false,
            Allergens: ["sesame"],
            Ingredients: ["quinoa", "chickpea", "kale", "avocado", "olive oil"],
            IsActive: true),
        new(
            CandidateCode: "ldc-ln-brown-rice-lentil-bowl",
            Title: "Cơm gạo lứt, đậu lăng hầm cà chua và bông cải xanh",
            Description: "Gạo lứt hạt tròn dùng kèm đậu lăng nấu sốt cà chua.",
            Category: "RiceBowl",
            Cuisine: "Vietnamese",
            SupportedSlots: [MealSlotType.Lunch, MealSlotType.Dinner],
            ServingSize: 1m,
            ServingUnit: "tô",
            CaloriesKcal: 510,
            ProteinGrams: 18.5m,
            CarbsGrams: 72m,
            FatGrams: 14m,
            SaturatedFatGrams: 2.0m,
            SugarGrams: 5.0m,
            FiberGrams: 12.5m,
            IsPlantBased: true,
            HasLegumes: true,
            HasWholeGrains: true,
            ContainsFish: false,
            Allergens: [],
            Ingredients: ["brown rice", "lentil", "tomato", "broccoli", "olive oil"],
            IsActive: true),
        new(
            CandidateCode: "ldc-ln-salmon-buckwheat-greens",
            Title: "Cá hồi áp chảo, mì kiều mạch và rau cải xanh",
            Description: "Cá hồi giàu omega-3 dùng kèm mì kiều mạch.",
            Category: "FishPlate",
            Cuisine: "AsianFusion",
            SupportedSlots: [MealSlotType.Lunch, MealSlotType.Dinner],
            ServingSize: 1m,
            ServingUnit: "khẩu phần",
            CaloriesKcal: 530,
            ProteinGrams: 24m,
            CarbsGrams: 48m,
            FatGrams: 21m,
            SaturatedFatGrams: 3.2m,
            SugarGrams: 3.5m,
            FiberGrams: 7.5m,
            IsPlantBased: false,
            HasLegumes: false,
            HasWholeGrains: true,
            ContainsFish: true,
            Allergens: ["fish", "soy"],
            Ingredients: ["salmon", "buckwheat", "bok choy", "olive oil"],
            IsActive: true),
        new(
            CandidateCode: "ldc-ln-inactive-dish",
            Title: "Món ngừng phục vụ",
            Description: "Món không còn hoạt động trong danh mục.",
            Category: "Bowl",
            Cuisine: "Vietnamese",
            SupportedSlots: [MealSlotType.Lunch],
            ServingSize: 1m,
            ServingUnit: "tô",
            CaloriesKcal: 400,
            ProteinGrams: 15m,
            CarbsGrams: 50m,
            FatGrams: 12m,
            SaturatedFatGrams: 1.5m,
            SugarGrams: 3.0m,
            FiberGrams: 10m,
            IsPlantBased: true,
            HasLegumes: true,
            HasWholeGrains: true,
            ContainsFish: false,
            Allergens: [],
            Ingredients: ["brown rice", "beans"],
            IsActive: false)
    ];

    [Fact]
    public void Execute_EnforcesHardConstraintPrecedence_OverPreferredTokens()
    {
        var candidates = CreateSampleCandidates();
        var request = new RecommendationRankRequest(
            ContractVersion: "2026-04-v2",
            RuleSetVersion: "LDC-RULES-2026.1",
            WeightProfileVersion: "LDC-WEIGHTS-V2.0",
            TargetSlot: MealSlotType.Lunch,
            DietaryPattern: "PlantForward",
            UserAgeYears: 40,
            PreferredCuisine: "Mediterranean",
            Allergies: ["sesame"],
            ExplicitExclusions: ["salmon"],
            DislikedTokens: [],
            PreferredTokens: ["ldc-ln-chickpea-quinoa-bowl", "ldc-ln-salmon-buckwheat-greens"],
            CurrentCandidateCode: null,
            Candidates: candidates);

        var result = RecommendationRankingPipeline.Execute(request);

        Assert.True(result.Succeeded);
        Assert.Single(result.AcceptedCandidates);
        Assert.Equal("ldc-ln-brown-rice-lentil-bowl", result.AcceptedCandidates[0].CandidateCode);

        Assert.Contains(
            result.RejectedCandidates,
            r => r.CandidateCode == "ldc-ln-chickpea-quinoa-bowl" &&
                 r.RejectionReasonCodes.Contains("REJECTED_HARD_ALLERGEN"));
        Assert.Contains(
            result.RejectedCandidates,
            r => r.CandidateCode == "ldc-ln-salmon-buckwheat-greens" &&
                 r.RejectionReasonCodes.Contains("REJECTED_EXPLICIT_EXCLUSION"));
        Assert.Contains(
            result.RejectedCandidates,
            r => r.CandidateCode == "ldc-ln-inactive-dish" &&
                 r.RejectionReasonCodes.Contains("REJECTED_INACTIVE_CANDIDATE"));
    }

    [Fact]
    public void Execute_DistinguishesSoftDislikeFromExplicitExclusion_WithoutMutatingWeights()
    {
        var candidates = CreateSampleCandidates();
        var baselineWeights = RecommendationSoftWeightProfile.DefaultV2;

        var requestWithDislike = new RecommendationRankRequest(
            ContractVersion: "2026-04-v2",
            RuleSetVersion: "LDC-RULES-2026.1",
            WeightProfileVersion: "LDC-WEIGHTS-V2.0",
            TargetSlot: MealSlotType.Lunch,
            DietaryPattern: "PlantForward",
            UserAgeYears: 40,
            PreferredCuisine: null,
            Allergies: [],
            ExplicitExclusions: [],
            DislikedTokens: ["ldc-ln-brown-rice-lentil-bowl"],
            PreferredTokens: [],
            CurrentCandidateCode: null,
            Candidates: candidates);

        var dislikeResult = RecommendationRankingPipeline.Execute(requestWithDislike);

        var dislikedCandidate = Assert.Single(
            dislikeResult.AcceptedCandidates,
            c => c.CandidateCode == "ldc-ln-brown-rice-lentil-bowl");
        Assert.True(dislikedCandidate.IsDisliked);
        Assert.Contains("SOFT_DISLIKE_PENALTY", dislikedCandidate.ReasonCodes);
        Assert.Equal("ldc-ln-chickpea-quinoa-bowl", dislikeResult.AcceptedCandidates[0].CandidateCode);

        var requestWithExclusion = requestWithDislike with
        {
            ExplicitExclusions = ["ldc-ln-brown-rice-lentil-bowl"],
            DislikedTokens = [],
        };

        var exclusionResult = RecommendationRankingPipeline.Execute(requestWithExclusion);
        Assert.DoesNotContain(
            exclusionResult.AcceptedCandidates,
            c => c.CandidateCode == "ldc-ln-brown-rice-lentil-bowl");
        Assert.Contains(
            exclusionResult.RejectedCandidates,
            r => r.CandidateCode == "ldc-ln-brown-rice-lentil-bowl" &&
                 r.RejectionReasonCodes.Contains("REJECTED_EXPLICIT_EXCLUSION"));

        Assert.Equal(baselineWeights, RecommendationSoftWeightProfile.DefaultV2);
    }

    [Fact]
    public void Execute_ProducesDeterministicStableTieBreak_AcrossPermutations()
    {
        var candidates = CreateSampleCandidates();
        var forwardRequest = new RecommendationRankRequest(
            ContractVersion: "2026-04-v2",
            RuleSetVersion: "LDC-RULES-2026.1",
            WeightProfileVersion: "LDC-WEIGHTS-V2.0",
            TargetSlot: MealSlotType.Lunch,
            DietaryPattern: "PlantForward",
            UserAgeYears: 45,
            PreferredCuisine: null,
            Allergies: [],
            ExplicitExclusions: [],
            DislikedTokens: [],
            PreferredTokens: [],
            CurrentCandidateCode: null,
            Candidates: candidates);

        var reversedRequest = forwardRequest with
        {
            Candidates = candidates.AsEnumerable().Reverse().ToList(),
        };

        var firstRun = RecommendationRankingPipeline.Execute(forwardRequest);
        var secondRun = RecommendationRankingPipeline.Execute(reversedRequest);

        Assert.Equal(
            firstRun.AcceptedCandidates.Select(x => (x.Rank, x.CandidateCode, x.FinalScore)),
            secondRun.AcceptedCandidates.Select(x => (x.Rank, x.CandidateCode, x.FinalScore)));
        Assert.Equal(
            firstRun.RejectedCandidates.Select(x => x.CandidateCode),
            secondRun.RejectedCandidates.Select(x => x.CandidateCode));
    }

    [Fact]
    public async Task SafeExplanationRewriter_FallsBackOnTimeoutOrInvalidMedicalClaim_AndNeverMutatesRankOrScore()
    {
        var baseResult = RecommendationRankingPipeline.Execute(new RecommendationRankRequest(
            ContractVersion: "2026-04-v2",
            RuleSetVersion: "LDC-RULES-2026.1",
            WeightProfileVersion: "LDC-WEIGHTS-V2.0",
            TargetSlot: MealSlotType.Lunch,
            DietaryPattern: "PlantForward",
            UserAgeYears: 45,
            PreferredCuisine: null,
            Allergies: [],
            ExplicitExclusions: [],
            DislikedTokens: [],
            PreferredTokens: [],
            CurrentCandidateCode: null,
            Candidates: CreateSampleCandidates()));

        var originalSnapshot = baseResult.AcceptedCandidates
            .Select(c => (c.Rank, c.CandidateCode, c.FinalScore, Codes: string.Join(',', c.ReasonCodes)))
            .ToList();

        // 1. Timeout generator -> falls back to deterministic template
        var timeoutRewriter = new SafeExplanationRewriter(
            new FakeExplanationGenerator(async (_, _, ct) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
                return "Never reached";
            }),
            TimeSpan.FromMilliseconds(50));

        var afterTimeout = await timeoutRewriter.ApplyAsync(baseResult, "PlantForward");
        Assert.All(afterTimeout.AcceptedCandidates, c =>
        {
            Assert.Equal("DETERMINISTIC_FALLBACK_TIMEOUT", c.ExplanationSource);
            Assert.Equal(c.DeterministicExplanation, c.ExplanationText);
        });
        Assert.Equal(
            originalSnapshot,
            afterTimeout.AcceptedCandidates.Select(c => (c.Rank, c.CandidateCode, c.FinalScore, Codes: string.Join(',', c.ReasonCodes))).ToList());

        // 2. Prohibited medical claim -> rejected by output validator, falls back to deterministic template
        var unsafeMedicalRewriter = new SafeExplanationRewriter(
            new FakeExplanationGenerator((candidate, _, _) =>
                Task.FromResult<string?>($"{candidate.Title} giúp chữa khỏi tiểu đường và kéo dài tuổi thọ thêm 20 năm.")));

        var afterUnsafeMedical = await unsafeMedicalRewriter.ApplyAsync(baseResult, "PlantForward");
        Assert.All(afterUnsafeMedical.AcceptedCandidates, c =>
        {
            Assert.Equal("DETERMINISTIC_FALLBACK", c.ExplanationSource);
            Assert.Equal(c.DeterministicExplanation, c.ExplanationText);
        });

        // 3. Valid rewrite -> updates ExplanationText only, never mutates Rank, FinalScore, or ReasonCodes
        var validRewriter = new SafeExplanationRewriter(
            new FakeExplanationGenerator((candidate, _, _) =>
                Task.FromResult<string?>($"Vì sao chọn {candidate.Title}: giàu đạm thực vật, chất xơ tự nhiên và phù hợp bữa ăn hằng ngày.")));

        var afterValid = await validRewriter.ApplyAsync(baseResult, "PlantForward");
        Assert.All(afterValid.AcceptedCandidates, c => Assert.Equal("AI_REWRITTEN_VALIDATED", c.ExplanationSource));
        Assert.Equal(
            originalSnapshot,
            afterValid.AcceptedCandidates.Select(c => (c.Rank, c.CandidateCode, c.FinalScore, Codes: string.Join(',', c.ReasonCodes))).ToList());
    }

    private sealed class FakeExplanationGenerator(
        Func<RankedRecommendationCandidate, string, CancellationToken, Task<string?>> handler)
        : IRecommendationExplanationTextGenerator
    {
        public Task<string?> TryRewriteExplanationAsync(
            RankedRecommendationCandidate candidate,
            string dietaryPattern,
            CancellationToken cancellationToken = default) =>
            handler(candidate, dietaryPattern, cancellationToken);
    }
}

