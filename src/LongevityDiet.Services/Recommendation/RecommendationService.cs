using System.Globalization;
using LongevityDiet.Domain.Entities;
using LongevityDiet.Domain.Recommendation;
using LongevityDiet.Repositories.Repositories;
using LongevityDiet.Services.Identity;
using LongevityDiet.Services.Planning;

namespace LongevityDiet.Services.Recommendation;

public sealed class RecommendationService(
    IMealPlanRepository mealPlanRepository,
    IRecommendationRepository recommendationRepository,
    IPlanningInputProvider planningInputProvider,
    IRecommendationEngineClient recommendationEngineClient,
    SafeExplanationRewriter explanationRewriter,
    TimeProvider timeProvider)
{
    public async Task<ServiceResult<RecommendationCatalogResultSummary>> GetRecommendationsAsync(
        Guid userId,
        GetMealRecommendationsQuery query,
        CancellationToken cancellationToken = default)
    {
        var userContext = await planningInputProvider.GetUserContextAsync(userId, cancellationToken);
        if (userContext is null)
        {
            return ServiceResult.Failure<RecommendationCatalogResultSummary>(
                "user_not_found",
                "User was not found.");
        }

        var targetSlot = MealSlotType.Lunch;
        if (!string.IsNullOrWhiteSpace(query.SlotType))
        {
            if (!Enum.TryParse<MealSlotType>(query.SlotType.Trim(), ignoreCase: true, out targetSlot))
            {
                return ServiceResult.Failure<RecommendationCatalogResultSummary>(
                    "invalid_slot_type",
                    "SlotType must be Breakfast, Lunch, Dinner, or Snack.");
            }
        }

        string? currentCandidateCode = null;
        var planAllergies = Array.Empty<string>();
        var planExclusions = Array.Empty<string>();

        if (query.PlannedMealId.HasValue)
        {
            var plan = query.MealPlanId.HasValue
                ? await mealPlanRepository.GetByIdForUserAsync(query.MealPlanId.Value, userId, cancellationToken)
                : await mealPlanRepository.GetActiveByUserAsync(userId, cancellationToken);

            if (plan is null)
            {
                return ServiceResult.Failure<RecommendationCatalogResultSummary>(
                    "meal_plan_not_found",
                    "Meal plan was not found for the current user.");
            }

            var plannedMeal = plan.Days
                .SelectMany(d => d.Meals)
                .FirstOrDefault(m => m.Id == query.PlannedMealId.Value);

            if (plannedMeal is null)
            {
                return ServiceResult.Failure<RecommendationCatalogResultSummary>(
                    "planned_meal_not_found",
                    "Planned meal was not found in the meal plan.");
            }

            targetSlot = plannedMeal.SlotType;
            currentCandidateCode = plannedMeal.CandidateCode;
            planAllergies = PlanningCatalogSnapshotProvider.SplitTokens(plan.AppliedAllergiesCsv).ToArray();
            planExclusions = PlanningCatalogSnapshotProvider.SplitTokens(plan.AppliedExclusionsCsv).ToArray();
        }

        var preferences = await recommendationRepository.ListFoodPreferencesAsync(userId, cancellationToken);
        var feedbacks = await recommendationRepository.ListFeedbackByUserAsync(userId, 100, cancellationToken);
        var ruleSet = await planningInputProvider.GetActiveRuleSetAsync(cancellationToken);
        var candidates = await planningInputProvider.GetCandidateMealsAsync(cancellationToken);

        var (allergies, explicitExclusions, dislikedTokens, preferredTokens) = BuildConstraintAndPreferenceSets(
            userContext,
            planAllergies,
            planExclusions,
            query.AdditionalExcludedCodes,
            preferences,
            feedbacks);

        var now = timeProvider.GetUtcNow();
        var userAge = userContext.BirthYear.HasValue
            ? Math.Clamp(now.Year - userContext.BirthYear.Value, 18, 110)
            : 35;

        var engineRequest = new RecommendationRankRequest(
            ContractVersion: "2026-04-v2",
            RuleSetVersion: ruleSet.RuleSetVersion,
            WeightProfileVersion: RecommendationSoftWeightProfile.DefaultV2.Version,
            TargetSlot: targetSlot,
            DietaryPattern: userContext.DietaryPattern,
            UserAgeYears: userAge,
            PreferredCuisine: userContext.PreferredCuisine,
            Allergies: allergies,
            ExplicitExclusions: explicitExclusions,
            DislikedTokens: dislikedTokens,
            PreferredTokens: preferredTokens,
            CurrentCandidateCode: currentCandidateCode,
            Candidates: MapCandidatesToEngineInput(candidates));

        var rankResult = await recommendationEngineClient.RankMealsAsync(engineRequest, cancellationToken);
        if (!rankResult.Succeeded)
        {
            return ServiceResult.Failure<RecommendationCatalogResultSummary>(
                rankResult.ErrorCode ?? "recommendation_ranking_failed",
                rankResult.ErrorMessage ?? "Could not rank meal recommendations.");
        }

        var enriched = await explanationRewriter.ApplyAsync(
            rankResult,
            userContext.DietaryPattern,
            cancellationToken);

        return ServiceResult.Success(new RecommendationCatalogResultSummary(
            ContractVersion: enriched.ContractVersion,
            RuleSetVersion: enriched.RuleSetVersion,
            WeightProfileVersion: enriched.WeightProfileVersion,
            TargetSlot: enriched.TargetSlot.ToString(),
            DietaryPattern: userContext.DietaryPattern,
            AppliedAllergies: allergies,
            AppliedExclusions: explicitExclusions,
            DislikedTokens: dislikedTokens,
            PreferredTokens: preferredTokens,
            AcceptedCandidates: enriched.AcceptedCandidates,
            RejectedCandidates: enriched.RejectedCandidates));
    }

    public async Task<ServiceResult<ReplacePlannedMealResultSummary>> ReplacePlannedMealAsync(
        Guid userId,
        ReplacePlannedMealCommand command,
        CancellationToken cancellationToken = default)
    {
        var plan = await mealPlanRepository.GetByIdForUserAsync(command.MealPlanId, userId, cancellationToken);
        if (plan is null)
        {
            return ServiceResult.Failure<ReplacePlannedMealResultSummary>(
                "meal_plan_not_found",
                "Meal plan was not found or does not belong to the current user.");
        }

        if (plan.Status != MealPlanStatus.Active)
        {
            return ServiceResult.Failure<ReplacePlannedMealResultSummary>(
                "meal_plan_conflict",
                "Only an active meal plan can have its meals replaced.");
        }

        var targetDay = plan.Days.FirstOrDefault(d => d.Meals.Any(m => m.Id == command.PlannedMealId));
        var targetMeal = targetDay?.Meals.FirstOrDefault(m => m.Id == command.PlannedMealId);
        if (targetDay is null || targetMeal is null)
        {
            return ServiceResult.Failure<ReplacePlannedMealResultSummary>(
                "planned_meal_not_found",
                "Planned meal was not found in the specified meal plan.");
        }

        var userContext = await planningInputProvider.GetUserContextAsync(userId, cancellationToken);
        if (userContext is null)
        {
            return ServiceResult.Failure<ReplacePlannedMealResultSummary>(
                "user_not_found",
                "User was not found.");
        }

        var preferences = await recommendationRepository.ListFoodPreferencesAsync(userId, cancellationToken);
        var feedbacks = await recommendationRepository.ListFeedbackByUserAsync(userId, 100, cancellationToken);
        var ruleSet = await planningInputProvider.GetActiveRuleSetAsync(cancellationToken);
        var candidates = await planningInputProvider.GetCandidateMealsAsync(cancellationToken);

        var planAllergies = PlanningCatalogSnapshotProvider.SplitTokens(plan.AppliedAllergiesCsv);
        var planExclusions = PlanningCatalogSnapshotProvider.SplitTokens(plan.AppliedExclusionsCsv);

        var (allergies, explicitExclusions, dislikedTokens, preferredTokens) = BuildConstraintAndPreferenceSets(
            userContext,
            planAllergies,
            planExclusions,
            command.AdditionalExcludedCodes,
            preferences,
            feedbacks);

        var now = timeProvider.GetUtcNow();
        var userAge = userContext.BirthYear.HasValue
            ? Math.Clamp(now.Year - userContext.BirthYear.Value, 18, 110)
            : 35;

        var engineRequest = new RecommendationRankRequest(
            ContractVersion: "2026-04-v2",
            RuleSetVersion: ruleSet.RuleSetVersion,
            WeightProfileVersion: RecommendationSoftWeightProfile.DefaultV2.Version,
            TargetSlot: targetMeal.SlotType,
            DietaryPattern: userContext.DietaryPattern,
            UserAgeYears: userAge,
            PreferredCuisine: userContext.PreferredCuisine,
            Allergies: allergies,
            ExplicitExclusions: explicitExclusions,
            DislikedTokens: dislikedTokens,
            PreferredTokens: preferredTokens,
            CurrentCandidateCode: targetMeal.CandidateCode,
            Candidates: MapCandidatesToEngineInput(candidates));

        var rankResult = await recommendationEngineClient.RankMealsAsync(engineRequest, cancellationToken);
        if (!rankResult.Succeeded)
        {
            return ServiceResult.Failure<ReplacePlannedMealResultSummary>(
                rankResult.ErrorCode ?? "recommendation_ranking_failed",
                rankResult.ErrorMessage ?? "Could not evaluate replacement candidates.");
        }

        var enriched = await explanationRewriter.ApplyAsync(
            rankResult,
            userContext.DietaryPattern,
            cancellationToken);

        RankedRecommendationCandidate? selectedReplacement;
        if (!string.IsNullOrWhiteSpace(command.TargetCandidateCode))
        {
            var requestedCode = command.TargetCandidateCode.Trim();
            var rejectedMatch = enriched.RejectedCandidates.FirstOrDefault(
                r => string.Equals(r.CandidateCode, requestedCode, StringComparison.OrdinalIgnoreCase));

            if (rejectedMatch is not null)
            {
                return ServiceResult.Failure<ReplacePlannedMealResultSummary>(
                    "unsafe_replacement_candidate",
                    $"Món '{rejectedMatch.Title}' bị từ chối bởi ràng buộc an toàn ({string.Join(", ", rejectedMatch.RejectionReasonCodes)}): {rejectedMatch.RejectionExplanation}");
            }

            selectedReplacement = enriched.AcceptedCandidates.FirstOrDefault(
                a => string.Equals(a.CandidateCode, requestedCode, StringComparison.OrdinalIgnoreCase));

            if (selectedReplacement is null)
            {
                return ServiceResult.Failure<ReplacePlannedMealResultSummary>(
                    "replacement_candidate_not_found",
                    $"Không tìm thấy món '{requestedCode}' hợp lệ cho khung bữa {targetMeal.SlotType}.");
            }
        }
        else
        {
            if (enriched.AcceptedCandidates.Count == 0)
            {
                return ServiceResult.Failure<ReplacePlannedMealResultSummary>(
                    "no_safe_candidates",
                    "Không tìm thấy món ăn thay thế an toàn phù hợp với ràng buộc dị ứng và loại trừ của bạn.");
            }

            var alternatives = enriched.AcceptedCandidates
                .Where(c => !string.Equals(c.CandidateCode, targetMeal.CandidateCode, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var nextRegenIndex = targetMeal.RegenerationCount;
            selectedReplacement = alternatives.Count > 0
                ? alternatives[nextRegenIndex % alternatives.Count]
                : enriched.AcceptedCandidates[0];
        }

        var previousCode = targetMeal.CandidateCode;
        var previousTitle = targetMeal.Title;
        var previousScore = targetMeal.DeterministicScore;
        var nextRegenerationCount = targetMeal.RegenerationCount + 1;

        var reasonCode = NormalizeReasonCode(command.ReplacementReasonCode, "USER_REQUESTED_REPLACEMENT");
        var reasonSummary = string.IsNullOrWhiteSpace(command.ReasonNotes)
            ? selectedReplacement.ExplanationText
            : $"{command.ReasonNotes.Trim()} — {selectedReplacement.ExplanationText}";

        // Update planned meal + day + plan + audit history atomically in the same unit of work
        targetMeal.CandidateCode = selectedReplacement.CandidateCode;
        targetMeal.Title = selectedReplacement.Title;
        targetMeal.Description = selectedReplacement.Description;
        targetMeal.Cuisine = selectedReplacement.Cuisine;
        targetMeal.ServingSize = selectedReplacement.ServingSize;
        targetMeal.ServingUnit = selectedReplacement.ServingUnit;
        targetMeal.CaloriesKcal = selectedReplacement.CaloriesKcal;
        targetMeal.ProteinGrams = selectedReplacement.ProteinGrams;
        targetMeal.CarbsGrams = selectedReplacement.CarbsGrams;
        targetMeal.FatGrams = selectedReplacement.FatGrams;
        targetMeal.FiberGrams = selectedReplacement.FiberGrams;
        targetMeal.IsPlantBased = selectedReplacement.IsPlantBased;
        targetMeal.HasLegumes = selectedReplacement.HasLegumes;
        targetMeal.HasWholeGrains = selectedReplacement.HasWholeGrains;
        targetMeal.ContainsFish = selectedReplacement.ContainsFish;
        targetMeal.AllergensCsv = string.Join(',', selectedReplacement.Allergens);
        targetMeal.IngredientsCsv = string.Join(',', selectedReplacement.Ingredients);
        targetMeal.DeterministicScore = selectedReplacement.FinalScore;
        targetMeal.ReasonCodesCsv = string.Join(',', selectedReplacement.ReasonCodes);
        targetMeal.ExplanationSummary = selectedReplacement.ExplanationText;
        targetMeal.RegenerationCount = nextRegenerationCount;
        targetMeal.UpdatedAt = now;

        targetDay.TargetCaloriesKcal = targetDay.Meals.Sum(m => m.CaloriesKcal);
        plan.AppliedAllergiesCsv = string.Join(',', allergies);
        plan.AppliedExclusionsCsv = string.Join(',', explicitExclusions);
        plan.UpdatedAt = now;

        var auditRecord = new MealReplacementHistory
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MealPlanId = plan.Id,
            MealPlanDayId = targetDay.Id,
            PlannedMealId = targetMeal.Id,
            DayNumber = targetDay.DayNumber,
            SlotType = targetMeal.SlotType,
            PreviousCandidateCode = previousCode,
            PreviousTitle = previousTitle,
            PreviousScore = previousScore,
            ReplacementCandidateCode = selectedReplacement.CandidateCode,
            ReplacementTitle = selectedReplacement.Title,
            ReplacementScore = selectedReplacement.FinalScore,
            ReplacementReasonCode = reasonCode,
            ReasonSummary = reasonSummary.Length > 1000 ? reasonSummary[..1000] : reasonSummary,
            AppliedAllergiesCsv = string.Join(',', allergies),
            AppliedExclusionsCsv = string.Join(',', explicitExclusions),
            RuleSetVersion = enriched.RuleSetVersion,
            WeightProfileVersion = enriched.WeightProfileVersion,
            ReplacedAt = now,
        };

        await recommendationRepository.AddReplacementHistoryAsync(auditRecord, cancellationToken);
        await mealPlanRepository.SaveChangesAsync(cancellationToken);

        var updatedPlanResult = await mealPlanRepository.GetByIdForUserAsync(plan.Id, userId, cancellationToken);
        var updatedPlanSummary = MapPlanSummary(updatedPlanResult ?? plan);
        var replacedMealSummary = updatedPlanSummary.Days
            .SelectMany(d => d.Meals)
            .First(m => m.Id == targetMeal.Id);

        return ServiceResult.Success(new ReplacePlannedMealResultSummary(
            UpdatedPlan: updatedPlanSummary,
            ReplacedMeal: replacedMealSummary,
            ReplacementAudit: MapHistorySummary(auditRecord)));
    }

    public async Task<ServiceResult<RecommendationFeedbackSummary>> SubmitFeedbackAsync(
        Guid userId,
        SubmitRecommendationFeedbackCommand command,
        CancellationToken cancellationToken = default)
    {
        var userContext = await planningInputProvider.GetUserContextAsync(userId, cancellationToken);
        if (userContext is null)
        {
            return ServiceResult.Failure<RecommendationFeedbackSummary>(
                "user_not_found",
                "User was not found.");
        }

        if (string.IsNullOrWhiteSpace(command.CandidateCode))
        {
            return ServiceResult.Failure<RecommendationFeedbackSummary>(
                "invalid_candidate_code",
                "CandidateCode is required.");
        }

        if (!Enum.TryParse<RecommendationFeedbackType>(command.FeedbackType?.Trim(), ignoreCase: true, out var feedbackType))
        {
            return ServiceResult.Failure<RecommendationFeedbackSummary>(
                "invalid_feedback_type",
                "FeedbackType must be Like or Dislike.");
        }

        var candidates = await planningInputProvider.GetCandidateMealsAsync(cancellationToken);
        var ruleSet = await planningInputProvider.GetActiveRuleSetAsync(cancellationToken);

        var candidateCode = command.CandidateCode.Trim();
        var matchedCandidate = candidates.FirstOrDefault(
            c => string.Equals(c.CandidateCode, candidateCode, StringComparison.OrdinalIgnoreCase));

        var canonicalCode = matchedCandidate?.CandidateCode ?? candidateCode.ToLowerInvariant();
        var candidateTitle = matchedCandidate?.Title ?? canonicalCode;
        var defaultReason = feedbackType == RecommendationFeedbackType.Like
            ? "USER_LIKED_MEAL"
            : "USER_DISLIKED_MEAL";
        var reasonCode = NormalizeReasonCode(command.ReasonCode, defaultReason);
        var notes = string.IsNullOrWhiteSpace(command.Notes) ? null : command.Notes.Trim();
        var now = timeProvider.GetUtcNow();

        var feedbackEntity = new RecommendationFeedback
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CandidateCode = canonicalCode,
            CandidateTitle = candidateTitle,
            PlannedMealId = command.PlannedMealId,
            FeedbackType = feedbackType,
            ReasonCode = reasonCode,
            Notes = notes,
            RuleSetVersion = ruleSet.RuleSetVersion,
            WeightProfileVersion = RecommendationSoftWeightProfile.DefaultV2.Version,
            CreatedAt = now,
        };

        await recommendationRepository.AddFeedbackAsync(feedbackEntity, cancellationToken);

        // Sync user-scoped soft preference unless the token is already an ExplicitExclusion
        var existingPrefs = await recommendationRepository.ListFoodPreferencesAsync(userId, cancellationToken);
        var existingForToken = existingPrefs.FirstOrDefault(
            p => string.Equals(p.FoodToken, canonicalCode, StringComparison.OrdinalIgnoreCase));

        if (existingForToken is null || existingForToken.PreferenceKind != FoodPreferenceKind.ExplicitExclusion)
        {
            var mappedKind = feedbackType == RecommendationFeedbackType.Like
                ? FoodPreferenceKind.Preferred
                : FoodPreferenceKind.Disliked;

            await recommendationRepository.UpsertFoodPreferenceAsync(
                new UserFoodPreference
                {
                    UserId = userId,
                    FoodToken = canonicalCode,
                    DisplayLabel = candidateTitle,
                    PreferenceKind = mappedKind,
                    Source = "RecommendationFeedback",
                    Notes = notes,
                    UpdatedAt = now,
                },
                cancellationToken);
        }

        await recommendationRepository.SaveChangesAsync(cancellationToken);

        return ServiceResult.Success(MapFeedbackSummary(feedbackEntity));
    }

    public async Task<ServiceResult<IReadOnlyList<RecommendationFeedbackSummary>>> ListFeedbackAsync(
        Guid userId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var items = await recommendationRepository.ListFeedbackByUserAsync(userId, limit, cancellationToken);
        var summaries = items.Select(MapFeedbackSummary).ToList();
        return ServiceResult.Success<IReadOnlyList<RecommendationFeedbackSummary>>(summaries);
    }

    public async Task<ServiceResult<IReadOnlyList<UserFoodPreferenceSummary>>> ListFoodPreferencesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var items = await recommendationRepository.ListFoodPreferencesAsync(userId, cancellationToken);
        var summaries = items.Select(MapFoodPreferenceSummary).ToList();
        return ServiceResult.Success<IReadOnlyList<UserFoodPreferenceSummary>>(summaries);
    }

    public async Task<ServiceResult<UserFoodPreferenceSummary>> UpsertFoodPreferenceAsync(
        Guid userId,
        UpsertFoodPreferenceCommand command,
        CancellationToken cancellationToken = default)
    {
        var userContext = await planningInputProvider.GetUserContextAsync(userId, cancellationToken);
        if (userContext is null)
        {
            return ServiceResult.Failure<UserFoodPreferenceSummary>(
                "user_not_found",
                "User was not found.");
        }

        if (string.IsNullOrWhiteSpace(command.FoodToken))
        {
            return ServiceResult.Failure<UserFoodPreferenceSummary>(
                "invalid_food_token",
                "FoodToken is required.");
        }

        if (!Enum.TryParse<FoodPreferenceKind>(command.PreferenceKind?.Trim(), ignoreCase: true, out var preferenceKind))
        {
            return ServiceResult.Failure<UserFoodPreferenceSummary>(
                "invalid_preference_kind",
                "PreferenceKind must be Preferred, Disliked, or ExplicitExclusion.");
        }

        var normalizedToken = RecommendationRankingPipeline.NormalizeToken(command.FoodToken);
        var displayLabel = string.IsNullOrWhiteSpace(command.DisplayLabel)
            ? command.FoodToken.Trim()
            : command.DisplayLabel.Trim();
        var notes = string.IsNullOrWhiteSpace(command.Notes) ? null : command.Notes.Trim();
        var now = timeProvider.GetUtcNow();

        var entity = new UserFoodPreference
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FoodToken = normalizedToken,
            DisplayLabel = displayLabel,
            PreferenceKind = preferenceKind,
            Source = "UserSetting",
            Notes = notes,
            UpdatedAt = now,
        };

        await recommendationRepository.UpsertFoodPreferenceAsync(entity, cancellationToken);

        // Keep UserDietaryPreference.ExcludedFoodsCsv in sync when user adds/changes an ExplicitExclusion
        if (preferenceKind == FoodPreferenceKind.ExplicitExclusion)
        {
            var mergedExclusions = userContext.ExcludedFoods
                .Concat([normalizedToken])
                .Select(RecommendationRankingPipeline.NormalizeToken)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            await mealPlanRepository.UpsertDietaryPreferenceAsync(
                new UserDietaryPreference
                {
                    UserId = userId,
                    DietaryPattern = userContext.DietaryPattern,
                    AllergiesCsv = string.Join(',', userContext.Allergies),
                    ExcludedFoodsCsv = string.Join(',', mergedExclusions),
                    PreferredCuisine = userContext.PreferredCuisine,
                    UpdatedAt = now,
                },
                cancellationToken);
        }

        await recommendationRepository.SaveChangesAsync(cancellationToken);

        var allPrefs = await recommendationRepository.ListFoodPreferencesAsync(userId, cancellationToken);
        var saved = allPrefs.First(x => string.Equals(x.FoodToken, normalizedToken, StringComparison.OrdinalIgnoreCase));
        return ServiceResult.Success(MapFoodPreferenceSummary(saved));
    }

    public async Task<ServiceResult<bool>> RemoveFoodPreferenceAsync(
        Guid userId,
        string foodToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(foodToken))
        {
            return ServiceResult.Failure<bool>(
                "invalid_food_token",
                "FoodToken is required.");
        }

        var normalizedToken = RecommendationRankingPipeline.NormalizeToken(foodToken);
        var removed = await recommendationRepository.RemoveFoodPreferenceAsync(userId, normalizedToken, cancellationToken);
        if (!removed)
        {
            return ServiceResult.Failure<bool>(
                "food_preference_not_found",
                "Food preference entry was not found.");
        }

        await recommendationRepository.SaveChangesAsync(cancellationToken);
        return ServiceResult.Success(true);
    }

    public async Task<ServiceResult<IReadOnlyList<MealReplacementHistorySummary>>> ListReplacementHistoryAsync(
        Guid userId,
        Guid? mealPlanId = null,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var items = await recommendationRepository.ListReplacementHistoryAsync(userId, mealPlanId, limit, cancellationToken);
        var summaries = items.Select(MapHistorySummary).ToList();
        return ServiceResult.Success<IReadOnlyList<MealReplacementHistorySummary>>(summaries);
    }

    private static (
        List<string> Allergies,
        List<string> ExplicitExclusions,
        List<string> DislikedTokens,
        List<string> PreferredTokens) BuildConstraintAndPreferenceSets(
        PlanningUserContextSnapshot userContext,
        IEnumerable<string> planAllergies,
        IEnumerable<string> planExclusions,
        IEnumerable<string>? additionalExclusions,
        IReadOnlyList<UserFoodPreference> foodPreferences,
        IReadOnlyList<RecommendationFeedback> feedbacks)
    {
        var allergySet = RecommendationRankingPipeline.NormalizeSet(
            userContext.Allergies.Concat(planAllergies));

        var explicitExclusionTokens = userContext.ExcludedFoods
            .Concat(planExclusions)
            .Concat(additionalExclusions ?? [])
            .Concat(foodPreferences
                .Where(p => p.PreferenceKind == FoodPreferenceKind.ExplicitExclusion)
                .Select(p => p.FoodToken));

        var exclusionSet = RecommendationRankingPipeline.NormalizeSet(explicitExclusionTokens);

        var latestFeedbackByCandidate = feedbacks
            .GroupBy(f => RecommendationRankingPipeline.NormalizeToken(f.CandidateCode), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.CreatedAt).First().FeedbackType,
                StringComparer.OrdinalIgnoreCase);

        var dislikedSet = RecommendationRankingPipeline.NormalizeSet(
            foodPreferences
                .Where(p => p.PreferenceKind == FoodPreferenceKind.Disliked)
                .Select(p => p.FoodToken)
                .Concat(latestFeedbackByCandidate
                    .Where(kv => kv.Value == RecommendationFeedbackType.Dislike)
                    .Select(kv => kv.Key)));

        // Hard constraints always override soft preferences
        dislikedSet.ExceptWith(allergySet);
        dislikedSet.ExceptWith(exclusionSet);

        var preferredSet = RecommendationRankingPipeline.NormalizeSet(
            foodPreferences
                .Where(p => p.PreferenceKind == FoodPreferenceKind.Preferred)
                .Select(p => p.FoodToken)
                .Concat(latestFeedbackByCandidate
                    .Where(kv => kv.Value == RecommendationFeedbackType.Like)
                    .Select(kv => kv.Key)));

        preferredSet.ExceptWith(allergySet);
        preferredSet.ExceptWith(exclusionSet);
        preferredSet.ExceptWith(dislikedSet);

        return (
            Allergies: allergySet.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
            ExplicitExclusions: exclusionSet.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
            DislikedTokens: dislikedSet.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
            PreferredTokens: preferredSet.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList());
    }

    private static List<RecommendationCandidateInput> MapCandidatesToEngineInput(
        IReadOnlyList<PlanningCandidateMealSnapshot> candidates)
    {
        return candidates
            .Select(c => new RecommendationCandidateInput(
                CandidateCode: c.CandidateCode,
                Title: c.Title,
                Description: c.Description,
                Category: c.Category,
                Cuisine: c.Cuisine,
                SupportedSlots: c.SupportedSlots,
                ServingSize: c.ServingSize,
                ServingUnit: c.ServingUnit,
                CaloriesKcal: c.CaloriesKcal,
                ProteinGrams: c.ProteinGrams,
                CarbsGrams: c.CarbsGrams,
                FatGrams: c.FatGrams,
                SaturatedFatGrams: c.SaturatedFatGrams,
                SugarGrams: c.SugarGrams,
                FiberGrams: c.FiberGrams,
                IsPlantBased: c.IsPlantBased,
                HasLegumes: c.HasLegumes,
                HasWholeGrains: c.HasWholeGrains,
                ContainsFish: c.ContainsFish,
                Allergens: c.Allergens,
                Ingredients: c.Ingredients,
                IsActive: c.IsActive,
                HasSafetyGateViolation: false))
            .ToList();
    }

    private static string NormalizeReasonCode(string? rawReason, string fallback)
    {
        if (string.IsNullOrWhiteSpace(rawReason))
        {
            return fallback;
        }

        var cleaned = rawReason.Trim().Replace(' ', '_').ToUpperInvariant();
        return cleaned.Length > 80 ? cleaned[..80] : cleaned;
    }

    private static RecommendationFeedbackSummary MapFeedbackSummary(RecommendationFeedback entity) =>
        new(
            entity.Id,
            entity.UserId,
            entity.CandidateCode,
            entity.CandidateTitle,
            entity.PlannedMealId,
            entity.FeedbackType.ToString(),
            entity.ReasonCode,
            entity.Notes,
            entity.RuleSetVersion,
            entity.WeightProfileVersion,
            entity.CreatedAt);

    private static UserFoodPreferenceSummary MapFoodPreferenceSummary(UserFoodPreference entity) =>
        new(
            entity.Id,
            entity.UserId,
            entity.FoodToken,
            entity.DisplayLabel,
            entity.PreferenceKind.ToString(),
            entity.Source,
            entity.Notes,
            entity.UpdatedAt);

    private static MealReplacementHistorySummary MapHistorySummary(MealReplacementHistory entity) =>
        new(
            entity.Id,
            entity.UserId,
            entity.MealPlanId,
            entity.MealPlanDayId,
            entity.PlannedMealId,
            entity.DayNumber,
            entity.SlotType.ToString(),
            entity.PreviousCandidateCode,
            entity.PreviousTitle,
            entity.PreviousScore,
            entity.ReplacementCandidateCode,
            entity.ReplacementTitle,
            entity.ReplacementScore,
            entity.ReplacementReasonCode,
            entity.ReasonSummary,
            PlanningCatalogSnapshotProvider.SplitTokens(entity.AppliedAllergiesCsv),
            PlanningCatalogSnapshotProvider.SplitTokens(entity.AppliedExclusionsCsv),
            entity.RuleSetVersion,
            entity.WeightProfileVersion,
            entity.ReplacedAt);

    private static MealPlanSummary MapPlanSummary(MealPlan plan)
    {
        var days = plan.Days
            .OrderBy(d => d.DayNumber)
            .Select(d =>
            {
                var meals = d.Meals
                    .OrderBy(m => m.SlotOrder)
                    .Select(m => new PlannedMealSummary(
                        m.Id,
                        m.MealPlanDayId,
                        m.SlotOrder,
                        m.SlotType.ToString(),
                        m.PlannedTime,
                        m.CandidateCode,
                        m.Title,
                        m.Description,
                        m.Cuisine,
                        m.ServingSize,
                        m.ServingUnit,
                        m.CaloriesKcal,
                        m.ProteinGrams,
                        m.CarbsGrams,
                        m.FatGrams,
                        m.FiberGrams,
                        m.IsPlantBased,
                        m.HasLegumes,
                        m.HasWholeGrains,
                        m.ContainsFish,
                        PlanningCatalogSnapshotProvider.SplitTokens(m.AllergensCsv),
                        PlanningCatalogSnapshotProvider.SplitTokens(m.IngredientsCsv),
                        m.DeterministicScore,
                        PlanningCatalogSnapshotProvider.SplitTokens(m.ReasonCodesCsv)
                            .Select(x => x.ToUpperInvariant())
                            .ToList(),
                        m.ExplanationSummary,
                        m.RegenerationCount))
                    .ToList();

                return new MealPlanDaySummary(
                    d.Id,
                    d.DayNumber,
                    d.PlanDate,
                    d.TargetCaloriesKcal,
                    meals.Sum(x => x.CaloriesKcal),
                    d.DailyFocusCode,
                    d.DailyFocusTitle,
                    meals);
            })
            .ToList();

        return new MealPlanSummary(
            plan.Id,
            plan.UserId,
            plan.RuleSetVersion,
            plan.RevisionNumber,
            plan.PreviousPlanId,
            plan.Status.ToString(),
            plan.StartDate,
            plan.EndDate,
            plan.MealsPerDay,
            plan.TimeZone,
            plan.DietaryPattern,
            PlanningCatalogSnapshotProvider.SplitTokens(plan.AppliedAllergiesCsv),
            PlanningCatalogSnapshotProvider.SplitTokens(plan.AppliedExclusionsCsv),
            PlanningCatalogSnapshotProvider.SplitTokens(plan.ReasonCodesCsv)
                .Select(x => x.ToUpperInvariant())
                .ToList(),
            plan.PlanExplanation,
            plan.GeneratedAt,
            plan.UpdatedAt,
            days);
    }
}

