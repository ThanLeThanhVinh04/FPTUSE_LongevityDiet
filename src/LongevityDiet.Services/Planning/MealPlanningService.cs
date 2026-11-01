using System.Globalization;
using LongevityDiet.Domain.Entities;
using LongevityDiet.Repositories.Repositories;
using LongevityDiet.Services.Identity;

namespace LongevityDiet.Services.Planning;

public sealed class MealPlanningService(
    IMealPlanRepository mealPlanRepository,
    IPlanningInputProvider planningInputProvider,
    TimeProvider timeProvider)
{
    private static readonly HashSet<string> SupportedDietaryPatterns =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "PlantForward",
            "Pescatarian",
            "StrictlyPlantBased"
        };

    private static readonly (string Code, string Title)[] DailyFocusRotation =
    [
        ("PLANT_FOUNDATION", "Ưu tiên rau củ và chất béo lành mạnh từ thực vật"),
        ("LEGUME_CONSISTENCY", "Duy trì đậu và hạt làm nguồn đạm trung tâm"),
        ("WHOLE_GRAIN_BALANCE", "Chọn tinh bột phức hợp nguyên cám ổn định năng lượng"),
        ("LOW_SUGAR_RHYTHM", "Giữ lượng đường tinh luyện và chất béo bão hòa ở mức thấp"),
        ("EATING_WINDOW_SYNC", "Đồng bộ bữa ăn trong khung 11–12 giờ"),
        ("EVENING_LIGHTNESS", "Bữa tối nhẹ nhàng, hoàn tất sớm trước giờ ngủ"),
        ("SUSTAINABLE_VARIETY", "Đa dạng hóa thực vật theo mùa và khẩu vị truyền thống")
    ];

    public async Task<ServiceResult<DietaryPreferenceSummary>> GetDietaryPreferenceAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var context = await planningInputProvider.GetUserContextAsync(userId, cancellationToken);
        if (context is null)
        {
            return ServiceResult.Failure<DietaryPreferenceSummary>(
                "user_not_found",
                "User was not found.");
        }

        var existing = await mealPlanRepository.GetDietaryPreferenceAsync(userId, cancellationToken);
        return ServiceResult.Success(new DietaryPreferenceSummary(
            userId,
            context.DietaryPattern,
            context.Allergies,
            context.ExcludedFoods,
            context.PreferredCuisine,
            existing?.UpdatedAt ?? timeProvider.GetUtcNow()));
    }

    public async Task<ServiceResult<DietaryPreferenceSummary>> UpdateDietaryPreferenceAsync(
        Guid userId,
        UpdateDietaryPreferenceCommand command,
        CancellationToken cancellationToken = default)
    {
        var context = await planningInputProvider.GetUserContextAsync(userId, cancellationToken);
        if (context is null)
        {
            return ServiceResult.Failure<DietaryPreferenceSummary>(
                "user_not_found",
                "User was not found.");
        }

        var pattern = NormalizeDietaryPattern(command.DietaryPattern);
        if (pattern is null)
        {
            return ServiceResult.Failure<DietaryPreferenceSummary>(
                "invalid_dietary_pattern",
                "Dietary pattern must be PlantForward, Pescatarian, or StrictlyPlantBased.");
        }

        var allergies = NormalizeTokens(command.Allergies);
        var exclusions = NormalizeTokens(command.ExcludedFoods);
        var cuisine = string.IsNullOrWhiteSpace(command.PreferredCuisine)
            ? null
            : command.PreferredCuisine.Trim();

        var now = timeProvider.GetUtcNow();
        var entity = new UserDietaryPreference
        {
            UserId = userId,
            DietaryPattern = pattern,
            AllergiesCsv = string.Join(',', allergies),
            ExcludedFoodsCsv = string.Join(',', exclusions),
            PreferredCuisine = cuisine,
            UpdatedAt = now,
        };

        await mealPlanRepository.UpsertDietaryPreferenceAsync(entity, cancellationToken);
        await mealPlanRepository.SaveChangesAsync(cancellationToken);

        return ServiceResult.Success(new DietaryPreferenceSummary(
            userId,
            pattern,
            allergies,
            exclusions,
            cuisine,
            now));
    }

    public async Task<ServiceResult<IReadOnlyList<PlanningCandidateMealSnapshot>>> GetActiveCandidatesAsync(
        CancellationToken cancellationToken = default)
    {
        var candidates = await planningInputProvider.GetCandidateMealsAsync(cancellationToken);
        var active = candidates
            .Where(x => x.IsActive)
            .OrderBy(x => x.CandidateCode, StringComparer.Ordinal)
            .ToList();

        return ServiceResult.Success<IReadOnlyList<PlanningCandidateMealSnapshot>>(active);
    }

    public async Task<ServiceResult<MealPlanSummary>> GetCurrentPlanAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var plan = await mealPlanRepository.GetActiveByUserAsync(userId, cancellationToken);
        if (plan is null)
        {
            return ServiceResult.Failure<MealPlanSummary>(
                "meal_plan_not_found",
                "No active meal plan was found for the current user.");
        }

        return ServiceResult.Success(ToSummary(plan));
    }

    public async Task<ServiceResult<MealPlanSummary>> GetPlanByIdAsync(
        Guid userId,
        Guid planId,
        CancellationToken cancellationToken = default)
    {
        var plan = await mealPlanRepository.GetByIdForUserAsync(planId, userId, cancellationToken);
        if (plan is null)
        {
            return ServiceResult.Failure<MealPlanSummary>(
                "meal_plan_not_found",
                "Meal plan was not found.");
        }

        return ServiceResult.Success(ToSummary(plan));
    }

    public async Task<ServiceResult<IReadOnlyList<MealPlanSummary>>> ListPlansAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var plans = await mealPlanRepository.ListByUserAsync(userId, cancellationToken);
        var summaries = plans.Select(ToSummary).ToList();
        return ServiceResult.Success<IReadOnlyList<MealPlanSummary>>(summaries);
    }

    public async Task<ServiceResult<MealPlanSummary>> GeneratePlanAsync(
        Guid userId,
        GenerateMealPlanCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.Days is < 7 or > 14)
        {
            return ServiceResult.Failure<MealPlanSummary>(
                "invalid_plan_days",
                "Meal plan duration must be between 7 and 14 days.");
        }

        var userContext = await planningInputProvider.GetUserContextAsync(userId, cancellationToken);
        if (userContext is null)
        {
            return ServiceResult.Failure<MealPlanSummary>(
                "user_not_found",
                "User was not found.");
        }

        var effectivePattern = command.DietaryPattern is not null
            ? NormalizeDietaryPattern(command.DietaryPattern)
            : userContext.DietaryPattern;

        if (effectivePattern is null)
        {
            return ServiceResult.Failure<MealPlanSummary>(
                "invalid_dietary_pattern",
                "Dietary pattern must be PlantForward, Pescatarian, or StrictlyPlantBased.");
        }

        var mergedAllergies = MergeTokens(userContext.Allergies, command.Allergies);
        var mergedExclusions = MergeTokens(userContext.ExcludedFoods, command.ExcludedFoods);
        var effectiveCuisine = !string.IsNullOrWhiteSpace(command.PreferredCuisine)
            ? command.PreferredCuisine.Trim()
            : userContext.PreferredCuisine;

        var now = timeProvider.GetUtcNow();

        if (command.DietaryPattern is not null ||
            command.Allergies is not null ||
            command.ExcludedFoods is not null ||
            command.PreferredCuisine is not null)
        {
            await mealPlanRepository.UpsertDietaryPreferenceAsync(
                new UserDietaryPreference
                {
                    UserId = userId,
                    DietaryPattern = effectivePattern,
                    AllergiesCsv = string.Join(',', mergedAllergies),
                    ExcludedFoodsCsv = string.Join(',', mergedExclusions),
                    PreferredCuisine = effectiveCuisine,
                    UpdatedAt = now,
                },
                cancellationToken);
        }

        var effectiveContext = userContext with
        {
            DietaryPattern = effectivePattern,
            Allergies = mergedAllergies,
            ExcludedFoods = mergedExclusions,
            PreferredCuisine = effectiveCuisine,
        };

        var ruleSet = await planningInputProvider.GetActiveRuleSetAsync(cancellationToken);
        var candidates = await planningInputProvider.GetCandidateMealsAsync(cancellationToken);

        var currentActive = await mealPlanRepository.GetActiveByUserAsync(userId, cancellationToken);
        var revisionNumber = currentActive is null ? 1 : currentActive.RevisionNumber + 1;
        var previousPlanId = currentActive?.Id;

        var startDate = command.StartDate ?? ResolveLocalDate(now, effectiveContext.TimeZone);

        var buildResult = BuildDeterministicPlan(
            effectiveContext,
            ruleSet,
            candidates,
            startDate,
            command.Days,
            revisionNumber,
            previousPlanId,
            preservedDays: [],
            now);

        if (!buildResult.Succeeded || buildResult.Value is null)
        {
            return ServiceResult.Failure<MealPlanSummary>(
                buildResult.ErrorCode ?? "plan_generation_failed",
                buildResult.ErrorMessage ?? "Could not generate meal plan.");
        }

        if (currentActive is not null)
        {
            currentActive.Status = MealPlanStatus.Superseded;
            currentActive.UpdatedAt = now;
        }

        await mealPlanRepository.AddAsync(buildResult.Value, cancellationToken);
        await mealPlanRepository.SaveChangesAsync(cancellationToken);

        return ServiceResult.Success(ToSummary(buildResult.Value));
    }

    public async Task<ServiceResult<MealPlanSummary>> RegeneratePlanAsync(
        Guid userId,
        Guid planId,
        RegenerateMealPlanCommand command,
        CancellationToken cancellationToken = default)
    {
        var existingPlan = await mealPlanRepository.GetByIdForUserAsync(planId, userId, cancellationToken);
        if (existingPlan is null)
        {
            return ServiceResult.Failure<MealPlanSummary>(
                "meal_plan_not_found",
                "Meal plan was not found.");
        }

        var userContext = await planningInputProvider.GetUserContextAsync(userId, cancellationToken);
        if (userContext is null)
        {
            return ServiceResult.Failure<MealPlanSummary>(
                "user_not_found",
                "User was not found.");
        }

        var ruleSet = await planningInputProvider.GetActiveRuleSetAsync(cancellationToken);
        var candidates = await planningInputProvider.GetCandidateMealsAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        var totalDays = existingPlan.Days.Count >= 7 ? existingPlan.Days.Count : 7;
        var fromDate = command.FromDate ?? existingPlan.StartDate;

        var preservedDays = existingPlan.Days
            .Where(d => d.PlanDate < fromDate)
            .OrderBy(d => d.DayNumber)
            .ToList();

        var newRevisionNumber = existingPlan.RevisionNumber + 1;

        var buildResult = BuildDeterministicPlan(
            userContext,
            ruleSet,
            candidates,
            existingPlan.StartDate,
            totalDays,
            newRevisionNumber,
            existingPlan.Id,
            preservedDays,
            now);

        if (!buildResult.Succeeded || buildResult.Value is null)
        {
            return ServiceResult.Failure<MealPlanSummary>(
                buildResult.ErrorCode ?? "plan_regeneration_failed",
                buildResult.ErrorMessage ?? "Could not regenerate meal plan.");
        }

        existingPlan.Status = MealPlanStatus.Superseded;
        existingPlan.UpdatedAt = now;

        await mealPlanRepository.AddAsync(buildResult.Value, cancellationToken);
        await mealPlanRepository.SaveChangesAsync(cancellationToken);

        return ServiceResult.Success(ToSummary(buildResult.Value));
    }

    public async Task<ServiceResult<MealPlanSummary>> RegeneratePlannedMealAsync(
        Guid userId,
        Guid planId,
        Guid plannedMealId,
        RegeneratePlannedMealCommand command,
        CancellationToken cancellationToken = default)
    {
        var plan = await mealPlanRepository.GetByIdForUserAsync(planId, userId, cancellationToken);
        if (plan is null)
        {
            return ServiceResult.Failure<MealPlanSummary>(
                "meal_plan_not_found",
                "Meal plan was not found.");
        }

        var targetDay = plan.Days.FirstOrDefault(d => d.Meals.Any(m => m.Id == plannedMealId));
        var targetMeal = targetDay?.Meals.FirstOrDefault(m => m.Id == plannedMealId);
        if (targetDay is null || targetMeal is null)
        {
            return ServiceResult.Failure<MealPlanSummary>(
                "planned_meal_not_found",
                "Planned meal was not found in the specified meal plan.");
        }

        var userContext = await planningInputProvider.GetUserContextAsync(userId, cancellationToken);
        if (userContext is null)
        {
            return ServiceResult.Failure<MealPlanSummary>(
                "user_not_found",
                "User was not found.");
        }

        var extraExclusions = NormalizeTokens(command.AdditionalExcludedCodes);
        var effectiveContext = userContext with
        {
            Allergies = MergeTokens(
                userContext.Allergies,
                PlanningCatalogSnapshotProvider.SplitTokens(plan.AppliedAllergiesCsv)),
            ExcludedFoods = MergeTokens(
                MergeTokens(
                    userContext.ExcludedFoods,
                    PlanningCatalogSnapshotProvider.SplitTokens(plan.AppliedExclusionsCsv)),
                extraExclusions),
        };

        var ruleSet = await planningInputProvider.GetActiveRuleSetAsync(cancellationToken);
        var candidates = await planningInputProvider.GetCandidateMealsAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        var rankedSlotCandidates = FilterAndRankCandidatesForSlot(
            candidates,
            effectiveContext,
            ruleSet,
            targetMeal.SlotType,
            now.Year);

        if (rankedSlotCandidates.Count == 0)
        {
            return ServiceResult.Failure<MealPlanSummary>(
                "no_safe_candidates",
                "Không tìm thấy món ăn thay thế an toàn phù hợp với ràng buộc dị ứng và loại trừ của bạn.");
        }

        var nextRegenerationCount = targetMeal.RegenerationCount + 1;
        var alternatives = rankedSlotCandidates
            .Where(x => !string.Equals(x.Candidate.CandidateCode, targetMeal.CandidateCode, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var chosen = alternatives.Count > 0
            ? alternatives[(targetDay.DayNumber - 1 + nextRegenerationCount - 1) % alternatives.Count]
            : rankedSlotCandidates[0];

        ApplyCandidateToPlannedMeal(targetMeal, chosen, ruleSet.RuleSetVersion, nextRegenerationCount, now);
        targetDay.TargetCaloriesKcal = targetDay.Meals.Sum(m => m.CaloriesKcal);
        plan.UpdatedAt = now;

        await mealPlanRepository.SaveChangesAsync(cancellationToken);

        return ServiceResult.Success(ToSummary(plan));
    }

    private static ServiceResult<MealPlan> BuildDeterministicPlan(
        PlanningUserContextSnapshot userContext,
        PlanningRuleSetSnapshot ruleSet,
        IReadOnlyList<PlanningCandidateMealSnapshot> candidates,
        DateOnly startDate,
        int daysCount,
        int revisionNumber,
        Guid? previousPlanId,
        IReadOnlyList<MealPlanDay> preservedDays,
        DateTimeOffset now)
    {
        var slots = ResolveMealSlots(
            userContext.PreferredMealFrequency,
            userContext.WakeTime ?? new TimeOnly(6, 30),
            userContext.SleepTime ?? new TimeOnly(22, 30));

        var rankedBySlot = new Dictionary<MealSlotType, IReadOnlyList<ScoredCandidate>>();
        foreach (var (slotType, _) in slots)
        {
            if (rankedBySlot.ContainsKey(slotType))
            {
                continue;
            }

            var ranked = FilterAndRankCandidatesForSlot(
                candidates,
                userContext,
                ruleSet,
                slotType,
                now.Year);

            if (ranked.Count == 0)
            {
                return ServiceResult.Failure<MealPlan>(
                    "no_safe_candidates",
                    $"Không có món ăn an toàn cho bữa {slotType} sau khi lọc dị ứng và thực phẩm loại trừ.");
            }

            rankedBySlot[slotType] = ranked;
        }

        var plan = new MealPlan
        {
            Id = Guid.NewGuid(),
            UserId = userContext.UserId,
            RuleSetVersion = ruleSet.RuleSetVersion,
            RevisionNumber = revisionNumber,
            PreviousPlanId = previousPlanId,
            Status = MealPlanStatus.Active,
            StartDate = startDate,
            EndDate = startDate.AddDays(daysCount - 1),
            MealsPerDay = slots.Count,
            TimeZone = userContext.TimeZone,
            DietaryPattern = userContext.DietaryPattern,
            AppliedAllergiesCsv = string.Join(',', userContext.Allergies),
            AppliedExclusionsCsv = string.Join(',', userContext.ExcludedFoods),
            ReasonCodesCsv = string.Join(',', ruleSet.Rules.Select(r => r.RuleKey)),
            PlanExplanation = BuildPlanExplanation(userContext, ruleSet),
            GeneratedAt = now,
            UpdatedAt = now,
        };

        var preservedByDate = preservedDays.ToDictionary(x => x.PlanDate);
        var rotationOffset = Math.Max(0, revisionNumber - 1);

        for (var dayIndex = 0; dayIndex < daysCount; dayIndex++)
        {
            var dayNumber = dayIndex + 1;
            var planDate = startDate.AddDays(dayIndex);
            var (focusCode, focusTitle) = DailyFocusRotation[dayIndex % DailyFocusRotation.Length];

            if (preservedByDate.TryGetValue(planDate, out var preservedDay))
            {
                var clonedDay = ClonePreservedDay(plan.Id, userContext.UserId, dayNumber, planDate, preservedDay, now);
                plan.Days.Add(clonedDay);
                continue;
            }

            var dayEntity = new MealPlanDay
            {
                Id = Guid.NewGuid(),
                MealPlanId = plan.Id,
                DayNumber = dayNumber,
                PlanDate = planDate,
                DailyFocusCode = focusCode,
                DailyFocusTitle = focusTitle,
            };

            for (var slotIndex = 0; slotIndex < slots.Count; slotIndex++)
            {
                var (slotType, plannedTime) = slots[slotIndex];
                var slotPool = rankedBySlot[slotType];
                var pickIndex = (dayIndex + rotationOffset + slotIndex) % slotPool.Count;
                var selected = slotPool[pickIndex];

                var plannedMeal = new PlannedMeal
                {
                    Id = Guid.NewGuid(),
                    MealPlanDayId = dayEntity.Id,
                    MealPlanId = plan.Id,
                    UserId = userContext.UserId,
                    SlotOrder = slotIndex + 1,
                    SlotType = slotType,
                    PlannedTime = plannedTime,
                };

                ApplyCandidateToPlannedMeal(plannedMeal, selected, ruleSet.RuleSetVersion, regenerationCount: 0, now);
                dayEntity.Meals.Add(plannedMeal);
            }

            dayEntity.TargetCaloriesKcal = dayEntity.Meals.Sum(x => x.CaloriesKcal);
            plan.Days.Add(dayEntity);
        }

        return ServiceResult.Success(plan);
    }

    public static IReadOnlyList<ScoredCandidate> FilterAndRankCandidatesForSlot(
        IReadOnlyList<PlanningCandidateMealSnapshot> candidates,
        PlanningUserContextSnapshot userContext,
        PlanningRuleSetSnapshot ruleSet,
        MealSlotType slotType,
        int currentYear)
    {
        var allergySet = new HashSet<string>(
            userContext.Allergies.Select(PlanningCatalogSnapshotProvider.NormalizeToken),
            StringComparer.OrdinalIgnoreCase);

        var exclusionSet = new HashSet<string>(
            userContext.ExcludedFoods.Select(PlanningCatalogSnapshotProvider.NormalizeToken),
            StringComparer.OrdinalIgnoreCase);

        var strictlyPlantBased = string.Equals(
            userContext.DietaryPattern,
            "StrictlyPlantBased",
            StringComparison.OrdinalIgnoreCase);

        var safeCandidates = candidates
            .Where(c => c.IsActive)
            .Where(c => c.SupportedSlots.Contains(slotType))
            .Where(c => !strictlyPlantBased || (c.IsPlantBased && !c.ContainsFish))
            .Where(c => !ViolatesHardConstraints(c, allergySet, exclusionSet))
            .Select(c => ScoreCandidate(c, userContext, ruleSet, currentYear))
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Candidate.CandidateCode, StringComparer.Ordinal)
            .ToList();

        return safeCandidates;
    }

    private static bool ViolatesHardConstraints(
        PlanningCandidateMealSnapshot candidate,
        HashSet<string> allergies,
        HashSet<string> exclusions)
    {
        if (allergies.Count == 0 && exclusions.Count == 0)
        {
            return false;
        }

        foreach (var allergen in candidate.Allergens)
        {
            var normalized = PlanningCatalogSnapshotProvider.NormalizeToken(allergen);
            if (allergies.Contains(normalized) || exclusions.Contains(normalized))
            {
                return true;
            }
        }

        foreach (var ingredient in candidate.Ingredients)
        {
            var normalized = PlanningCatalogSnapshotProvider.NormalizeToken(ingredient);
            if (allergies.Contains(normalized) || exclusions.Contains(normalized))
            {
                return true;
            }

            if (allergies.Any(a => normalized.Contains(a, StringComparison.OrdinalIgnoreCase)) ||
                exclusions.Any(e => normalized.Contains(e, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        var normalizedCode = PlanningCatalogSnapshotProvider.NormalizeToken(candidate.CandidateCode);
        var normalizedTitle = PlanningCatalogSnapshotProvider.NormalizeToken(candidate.Title);

        if (exclusions.Contains(normalizedCode))
        {
            return true;
        }

        foreach (var token in allergies.Concat(exclusions))
        {
            if (token.Length >= 3 &&
                (normalizedCode.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                 normalizedTitle.Contains(token, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static ScoredCandidate ScoreCandidate(
        PlanningCandidateMealSnapshot candidate,
        PlanningUserContextSnapshot userContext,
        PlanningRuleSetSnapshot ruleSet,
        int currentYear)
    {
        var score = 30m;
        var reasons = new List<string>();
        var weights = ruleSet.Rules.ToDictionary(
            x => x.RuleKey,
            x => x.BaseWeight,
            StringComparer.OrdinalIgnoreCase);

        if (candidate.IsPlantBased)
        {
            score += weights.GetValueOrDefault("PLANT_FORWARD", 25m);
            reasons.Add("PLANT_FORWARD");
        }

        if (candidate.HasLegumes)
        {
            score += weights.GetValueOrDefault("LEGUME_PROTEIN", 20m);
            reasons.Add("LEGUME_PROTEIN");
        }

        if (candidate.HasWholeGrains)
        {
            score += weights.GetValueOrDefault("COMPLEX_CARBS", 15m);
            reasons.Add("COMPLEX_CARBS");
        }

        if (candidate.SugarGrams <= 6.5m && candidate.SaturatedFatGrams <= 2.5m)
        {
            score += weights.GetValueOrDefault("LOW_SUGAR_SATFAT", 15m);
            reasons.Add("LOW_SUGAR_SATFAT");
        }
        else if (candidate.SugarGrams > 12m || candidate.SaturatedFatGrams > 5m)
        {
            score -= 12m;
        }

        if (candidate.ContainsFish &&
            (string.Equals(userContext.DietaryPattern, "Pescatarian", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(userContext.DietaryPattern, "PlantForward", StringComparison.OrdinalIgnoreCase)))
        {
            var fishBoost = string.Equals(userContext.DietaryPattern, "Pescatarian", StringComparison.OrdinalIgnoreCase)
                ? weights.GetValueOrDefault("MODERATE_FISH", 10m) + 8m
                : weights.GetValueOrDefault("MODERATE_FISH", 10m);
            score += fishBoost;
            reasons.Add("MODERATE_FISH");
        }

        var userAge = userContext.BirthYear.HasValue ? currentYear - userContext.BirthYear.Value : 35;
        if (userAge >= 65)
        {
            if (candidate.ProteinGrams >= 16m)
            {
                score += weights.GetValueOrDefault("AGE_PROTEIN_FIT", 10m);
                reasons.Add("AGE_PROTEIN_FIT");
            }
        }
        else if (candidate.ProteinGrams is >= 7m and <= 24m)
        {
            score += weights.GetValueOrDefault("AGE_PROTEIN_FIT", 10m);
            reasons.Add("AGE_PROTEIN_FIT");
        }

        if (!string.IsNullOrWhiteSpace(userContext.PreferredCuisine) &&
            string.Equals(candidate.Cuisine, userContext.PreferredCuisine, StringComparison.OrdinalIgnoreCase))
        {
            score += 12m;
            reasons.Add("CUISINE_MATCH");
        }

        if (candidate.FiberGrams >= 8m)
        {
            score += 5m;
            reasons.Add("HIGH_FIBER");
        }

        return new ScoredCandidate(
            candidate,
             decimal.Round(score, 2),
            reasons);
    }

    private static void ApplyCandidateToPlannedMeal(
        PlannedMeal meal,
        ScoredCandidate scored,
        string ruleSetVersion,
        int regenerationCount,
        DateTimeOffset now)
    {
        var c = scored.Candidate;
        meal.CandidateCode = c.CandidateCode;
        meal.Title = c.Title;
        meal.Description = c.Description;
        meal.Cuisine = c.Cuisine;
        meal.ServingSize = c.ServingSize;
        meal.ServingUnit = c.ServingUnit;
        meal.CaloriesKcal = c.CaloriesKcal;
        meal.ProteinGrams = c.ProteinGrams;
        meal.CarbsGrams = c.CarbsGrams;
        meal.FatGrams = c.FatGrams;
        meal.FiberGrams = c.FiberGrams;
        meal.IsPlantBased = c.IsPlantBased;
        meal.HasLegumes = c.HasLegumes;
        meal.HasWholeGrains = c.HasWholeGrains;
        meal.ContainsFish = c.ContainsFish;
        meal.AllergensCsv = string.Join(',', c.Allergens);
        meal.IngredientsCsv = string.Join(',', c.Ingredients);
        meal.DeterministicScore = scored.Score;
        meal.ReasonCodesCsv = string.Join(',', scored.ReasonCodes);
        meal.ExplanationSummary = BuildMealExplanation(c, scored.ReasonCodes, ruleSetVersion);
        meal.RegenerationCount = regenerationCount;
        meal.UpdatedAt = now;
    }

    private static MealPlanDay ClonePreservedDay(
        Guid newPlanId,
        Guid userId,
        int dayNumber,
        DateOnly planDate,
        MealPlanDay sourceDay,
        DateTimeOffset now)
    {
        var clonedDay = new MealPlanDay
        {
            Id = Guid.NewGuid(),
            MealPlanId = newPlanId,
            DayNumber = dayNumber,
            PlanDate = planDate,
            TargetCaloriesKcal = sourceDay.TargetCaloriesKcal,
            DailyFocusCode = sourceDay.DailyFocusCode,
            DailyFocusTitle = sourceDay.DailyFocusTitle,
        };

        foreach (var meal in sourceDay.Meals.OrderBy(m => m.SlotOrder))
        {
            clonedDay.Meals.Add(new PlannedMeal
            {
                Id = Guid.NewGuid(),
                MealPlanDayId = clonedDay.Id,
                MealPlanId = newPlanId,
                UserId = userId,
                SlotOrder = meal.SlotOrder,
                SlotType = meal.SlotType,
                PlannedTime = meal.PlannedTime,
                CandidateCode = meal.CandidateCode,
                Title = meal.Title,
                Description = meal.Description,
                Cuisine = meal.Cuisine,
                ServingSize = meal.ServingSize,
                ServingUnit = meal.ServingUnit,
                CaloriesKcal = meal.CaloriesKcal,
                ProteinGrams = meal.ProteinGrams,
                CarbsGrams = meal.CarbsGrams,
                FatGrams = meal.FatGrams,
                FiberGrams = meal.FiberGrams,
                IsPlantBased = meal.IsPlantBased,
                HasLegumes = meal.HasLegumes,
                HasWholeGrains = meal.HasWholeGrains,
                ContainsFish = meal.ContainsFish,
                AllergensCsv = meal.AllergensCsv,
                IngredientsCsv = meal.IngredientsCsv,
                DeterministicScore = meal.DeterministicScore,
                ReasonCodesCsv = meal.ReasonCodesCsv,
                ExplanationSummary = meal.ExplanationSummary,
                RegenerationCount = meal.RegenerationCount,
                UpdatedAt = now,
            });
        }

        return clonedDay;
    }

    private static List<(MealSlotType SlotType, TimeOnly PlannedTime)> ResolveMealSlots(
        int mealsPerDay,
        TimeOnly wakeTime,
        TimeOnly sleepTime)
    {
        var breakfastTime = AddMinutesClamped(wakeTime, 60, new TimeOnly(7, 30));
        var dinnerTime = SubtractMinutesBeforeSleep(sleepTime, 210, new TimeOnly(18, 30));

        if (dinnerTime <= breakfastTime)
        {
            breakfastTime = new TimeOnly(7, 30);
            dinnerTime = new TimeOnly(18, 30);
        }

        var windowSpanMinutes = (int)(dinnerTime - breakfastTime).TotalMinutes;
        var lunchTime = breakfastTime.AddMinutes(Math.Max(180, windowSpanMinutes / 2));
        var snackTime = breakfastTime.AddMinutes(Math.Max(300, (windowSpanMinutes * 3) / 4));

        return mealsPerDay switch
        {
            2 =>
            [
                (MealSlotType.Breakfast, breakfastTime),
                (MealSlotType.Dinner, dinnerTime)
            ],
            4 =>
            [
                (MealSlotType.Breakfast, breakfastTime),
                (MealSlotType.Lunch, lunchTime),
                (MealSlotType.Snack, snackTime),
                (MealSlotType.Dinner, dinnerTime)
            ],
            _ =>
            [
                (MealSlotType.Breakfast, breakfastTime),
                (MealSlotType.Lunch, lunchTime),
                (MealSlotType.Dinner, dinnerTime)
            ],
        };
    }

    private static TimeOnly AddMinutesClamped(TimeOnly baseTime, int minutesToAdd, TimeOnly fallback)
    {
        var totalMinutes = (baseTime.Hour * 60) + baseTime.Minute + minutesToAdd;
        if (totalMinutes is < 300 or > 660)
        {
            return fallback;
        }

        return new TimeOnly(totalMinutes / 60, totalMinutes % 60);
    }

    private static TimeOnly SubtractMinutesBeforeSleep(TimeOnly sleepTime, int minutesBefore, TimeOnly fallback)
    {
        var sleepMinutes = (sleepTime.Hour * 60) + sleepTime.Minute;
        if (sleepMinutes < 240)
        {
            sleepMinutes += 1440;
        }

        var targetMinutes = sleepMinutes - minutesBefore;
        if (targetMinutes is < 960 or > 1230)
        {
            return fallback;
        }

        return new TimeOnly((targetMinutes % 1440) / 60, targetMinutes % 60);
    }

    private static string BuildPlanExplanation(
        PlanningUserContextSnapshot userContext,
        PlanningRuleSetSnapshot ruleSet)
    {
        var allergyNote = userContext.Allergies.Count > 0
            ? $"Đã loại trừ cứng dị ứng: {string.Join(", ", userContext.Allergies)}."
            : "Không có dị ứng khai báo.";

        var exclusionNote = userContext.ExcludedFoods.Count > 0
            ? $"Đã loại trừ thực phẩm: {string.Join(", ", userContext.ExcludedFoods)}."
            : "Không có thực phẩm loại trừ riêng.";

        return string.Create(
            CultureInfo.InvariantCulture,
            $"Kế hoạch 7 ngày được tạo tất định theo bộ quy tắc {ruleSet.RuleSetVersion} (Five Pillars), chế độ {userContext.DietaryPattern}, {userContext.PreferredMealFrequency} bữa/ngày. {allergyNote} {exclusionNote}");
    }

    private static string BuildMealExplanation(
        PlanningCandidateMealSnapshot candidate,
        IReadOnlyList<string> reasonCodes,
        string ruleSetVersion)
    {
        var highlights = new List<string>();
        if (reasonCodes.Contains("PLANT_FORWARD", StringComparer.OrdinalIgnoreCase))
        {
            highlights.Add("nền tảng thực vật");
        }

        if (reasonCodes.Contains("LEGUME_PROTEIN", StringComparer.OrdinalIgnoreCase))
        {
            highlights.Add("giàu đạm từ các loại đậu");
        }

        if (reasonCodes.Contains("COMPLEX_CARBS", StringComparer.OrdinalIgnoreCase))
        {
            highlights.Add("ngũ cốc nguyên hạt nhiều chất xơ");
        }

        if (reasonCodes.Contains("MODERATE_FISH", StringComparer.OrdinalIgnoreCase))
        {
            highlights.Add("bổ sung cá giàu omega-3");
        }

        if (reasonCodes.Contains("LOW_SUGAR_SATFAT", StringComparer.OrdinalIgnoreCase))
        {
            highlights.Add("ít đường tinh luyện và chất béo bão hòa");
        }

        var highlightText = highlights.Count > 0
            ? string.Join(", ", highlights)
            : "cân bằng dinh dưỡng hằng ngày";

        return string.Create(
            CultureInfo.InvariantCulture,
            $"Chọn {candidate.Title} vì đáp ứng {highlightText} theo bộ quy tắc {ruleSetVersion} (an toàn với toàn bộ ràng buộc dị ứng/loại trừ).");
    }

    private static MealPlanSummary ToSummary(MealPlan plan)
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

    private static string? NormalizeDietaryPattern(string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return "PlantForward";
        }

        var trimmed = pattern.Trim();
        if (!SupportedDietaryPatterns.Contains(trimmed))
        {
            return null;
        }

        if (string.Equals(trimmed, "Pescatarian", StringComparison.OrdinalIgnoreCase))
        {
            return "Pescatarian";
        }

        if (string.Equals(trimmed, "StrictlyPlantBased", StringComparison.OrdinalIgnoreCase))
        {
            return "StrictlyPlantBased";
        }

        return "PlantForward";
    }

    private static List<string> NormalizeTokens(IReadOnlyList<string>? values)
    {
        if (values is null || values.Count == 0)
        {
            return [];
        }

        return values
            .SelectMany(PlanningCatalogSnapshotProvider.SplitTokens)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> MergeTokens(
        IReadOnlyList<string> first,
        IReadOnlyList<string>? second)
    {
        if (second is null || second.Count == 0)
        {
            return NormalizeTokens(first);
        }

        return NormalizeTokens(first.Concat(second).ToList());
    }

    public static DateOnly ResolveLocalDate(DateTimeOffset timestamp, string timeZoneId)
    {
        var tz = ResolveTimeZone(timeZoneId);
        var local = TimeZoneInfo.ConvertTime(timestamp, tz);
        return DateOnly.FromDateTime(local.DateTime);
    }

    public static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        var candidate = timeZoneId.Trim();
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(candidate);
        }
        catch (TimeZoneNotFoundException)
        {
            if (TimeZoneInfo.TryConvertIanaIdToWindowsId(candidate, out var windowsId))
            {
                return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
            }

            if (TimeZoneInfo.TryConvertWindowsIdToIanaId(candidate, out var ianaId))
            {
                return TimeZoneInfo.FindSystemTimeZoneById(ianaId);
            }

            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    public sealed record ScoredCandidate(
        PlanningCandidateMealSnapshot Candidate,
        decimal Score,
        IReadOnlyList<string> ReasonCodes);
}

