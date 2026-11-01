using LongevityDiet.Domain.Entities;
using LongevityDiet.Repositories.Repositories;
using LongevityDiet.Services.Planning;
using LongevityDiet.Services.Tracking;

namespace LongevityDiet.UnitTests;

public sealed class MealPlanningAndEatingWindowTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GeneratePlan_IsDeterministicAndReproducible_ForSameInputs()
    {
        var userId = Guid.NewGuid();
        var userRepo = CreateUserRepositoryWithOnboardedUser(userId);
        var planRepo1 = new InMemoryMealPlanRepository();
        var planRepo2 = new InMemoryMealPlanRepository();
        var timeProvider = new FixedTimeProvider(FixedNow);

        var service1 = new MealPlanningService(
            planRepo1,
            new PlanningCatalogSnapshotProvider(userRepo, planRepo1),
            timeProvider);

        var service2 = new MealPlanningService(
            planRepo2,
            new PlanningCatalogSnapshotProvider(userRepo, planRepo2),
            timeProvider);

        var command = new GenerateMealPlanCommand(
            StartDate: new DateOnly(2026, 11, 2),
            Days: 7,
            DietaryPattern: "PlantForward",
            Allergies: ["peanut"],
            ExcludedFoods: ["tofu"],
            PreferredCuisine: "Vietnamese");

        var first = await service1.GeneratePlanAsync(userId, command);
        var second = await service2.GeneratePlanAsync(userId, command);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(7, first.Value!.Days.Count);
        Assert.Equal(7, second.Value!.Days.Count);

        for (var d = 0; d < 7; d++)
        {
            var day1 = first.Value.Days[d];
            var day2 = second.Value.Days[d];
            Assert.Equal(day1.PlanDate, day2.PlanDate);
            Assert.Equal(day1.Meals.Count, day2.Meals.Count);

            for (var m = 0; m < day1.Meals.Count; m++)
            {
                Assert.Equal(day1.Meals[m].CandidateCode, day2.Meals[m].CandidateCode);
                Assert.Equal(day1.Meals[m].DeterministicScore, day2.Meals[m].DeterministicScore);
                Assert.Equal(day1.Meals[m].ReasonCodes, day2.Meals[m].ReasonCodes);
            }
        }
    }

    [Fact]
    public async Task GeneratePlan_EnforcesAllergyHardExclusion_NeverIncludesAllergenicCandidate()
    {
        var userId = Guid.NewGuid();
        var userRepo = CreateUserRepositoryWithOnboardedUser(userId, mealFrequency: 4);
        var planRepo = new InMemoryMealPlanRepository();
        var service = new MealPlanningService(
            planRepo,
            new PlanningCatalogSnapshotProvider(userRepo, planRepo),
            new FixedTimeProvider(FixedNow));

        var command = new GenerateMealPlanCommand(
            StartDate: new DateOnly(2026, 11, 2),
            Days: 7,
            DietaryPattern: "Pescatarian",
            Allergies: ["peanut", "shellfish", "soy", "tree-nut"],
            ExcludedFoods: []);

        var result = await service.GeneratePlanAsync(userId, command);

        Assert.True(result.Succeeded);
        var allMeals = result.Value!.Days.SelectMany(x => x.Meals).ToList();
        Assert.Equal(28, allMeals.Count);

        foreach (var meal in allMeals)
        {
            Assert.DoesNotContain("peanut", meal.Allergens, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain("shellfish", meal.Allergens, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain("soy", meal.Allergens, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain("tree-nut", meal.Allergens, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task GeneratePlan_EnforcesUserFoodExclusionAndStrictlyPlantBasedPattern()
    {
        var userId = Guid.NewGuid();
        var userRepo = CreateUserRepositoryWithOnboardedUser(userId, mealFrequency: 3);
        var planRepo = new InMemoryMealPlanRepository();
        var service = new MealPlanningService(
            planRepo,
            new PlanningCatalogSnapshotProvider(userRepo, planRepo),
            new FixedTimeProvider(FixedNow));

        var command = new GenerateMealPlanCommand(
            StartDate: new DateOnly(2026, 11, 2),
            Days: 7,
            DietaryPattern: "StrictlyPlantBased",
            Allergies: [],
            ExcludedFoods: ["brown-rice", "tofu"]);

        var result = await service.GeneratePlanAsync(userId, command);

        Assert.True(result.Succeeded);
        var allMeals = result.Value!.Days.SelectMany(x => x.Meals).ToList();

        foreach (var meal in allMeals)
        {
            Assert.True(meal.IsPlantBased);
            Assert.False(meal.ContainsFish);
            Assert.DoesNotContain("brown-rice", meal.Ingredients, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain("tofu", meal.Ingredients, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task RegeneratePlannedMeal_SelectsNextSafeCandidateAndIncrementsRegenerationCount()
    {
        var userId = Guid.NewGuid();
        var userRepo = CreateUserRepositoryWithOnboardedUser(userId, mealFrequency: 3);
        var planRepo = new InMemoryMealPlanRepository();
        var service = new MealPlanningService(
            planRepo,
            new PlanningCatalogSnapshotProvider(userRepo, planRepo),
            new FixedTimeProvider(FixedNow));

        var generated = await service.GeneratePlanAsync(
            userId,
            new GenerateMealPlanCommand(
                new DateOnly(2026, 11, 2),
                Days: 7,
                DietaryPattern: "PlantForward",
                Allergies: ["peanut", "shellfish"],
                ExcludedFoods: ["salmon"]));

        Assert.True(generated.Succeeded);
        var initialPlan = generated.Value!;
        var targetMeal = initialPlan.Days[0].Meals[0];

        var regenerated = await service.RegeneratePlannedMealAsync(
            userId,
            initialPlan.Id,
            targetMeal.Id,
            new RegeneratePlannedMealCommand());

        Assert.True(regenerated.Succeeded);
        var updatedMeal = regenerated.Value!.Days[0].Meals[0];
        Assert.Equal(1, updatedMeal.RegenerationCount);
        Assert.NotEqual(targetMeal.CandidateCode, updatedMeal.CandidateCode);
        Assert.DoesNotContain("peanut", updatedMeal.Allergens, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("shellfish", updatedMeal.Allergens, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("salmon", updatedMeal.Ingredients, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RegeneratePlan_SupersedesPreviousRevisionAndPreservesDaysBeforeFromDate()
    {
        var userId = Guid.NewGuid();
        var userRepo = CreateUserRepositoryWithOnboardedUser(userId, mealFrequency: 3);
        var planRepo = new InMemoryMealPlanRepository();
        var service = new MealPlanningService(
            planRepo,
            new PlanningCatalogSnapshotProvider(userRepo, planRepo),
            new FixedTimeProvider(FixedNow));

        var startDate = new DateOnly(2026, 11, 2);
        var first = await service.GeneratePlanAsync(
            userId,
            new GenerateMealPlanCommand(startDate, Days: 7));

        Assert.True(first.Succeeded);
        var firstDay1BreakfastCode = first.Value!.Days[0].Meals[0].CandidateCode;
        var firstDay4BreakfastCode = first.Value.Days[3].Meals[0].CandidateCode;

        var second = await service.RegeneratePlanAsync(
            userId,
            first.Value.Id,
            new RegenerateMealPlanCommand(FromDate: startDate.AddDays(2)));

        Assert.True(second.Succeeded);
        Assert.Equal(2, second.Value!.RevisionNumber);
        Assert.Equal(first.Value.Id, second.Value.PreviousPlanId);
        Assert.Equal(firstDay1BreakfastCode, second.Value.Days[0].Meals[0].CandidateCode);
        Assert.NotEqual(firstDay4BreakfastCode, second.Value.Days[3].Meals[0].CandidateCode);

        var allPlans = await service.ListPlansAsync(userId);
        Assert.Equal(2, allPlans.Value!.Count);
        Assert.Contains(allPlans.Value, p => p.Id == first.Value.Id && p.Status == "Superseded");
        Assert.Contains(allPlans.Value, p => p.Id == second.Value.Id && p.Status == "Active");
    }

    [Fact]
    public void EatingWindow_CalculatesDurationAndBedtimeGap_ForStandardSchedule()
    {
        var userId = Guid.NewGuid();
        var schedule = new UserScheduleSnapshot(
            "v1",
            userId,
            "Asia/Ho_Chi_Minh",
            new TimeOnly(6, 30),
            new TimeOnly(22, 30),
            720);

        var localDate = new DateOnly(2026, 11, 2);
        var logs = new List<MealLog>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                LocalDate = localDate,
                TimeZone = "Asia/Ho_Chi_Minh",
                EatenAt = new DateTimeOffset(2026, 11, 2, 7, 30, 0, TimeSpan.FromHours(7)),
                TotalCaloriesKcal = 350,
            },
            new()
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                LocalDate = localDate,
                TimeZone = "Asia/Ho_Chi_Minh",
                EatenAt = new DateTimeOffset(2026, 11, 2, 12, 30, 0, TimeSpan.FromHours(7)),
                TotalCaloriesKcal = 520,
            },
            new()
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                LocalDate = localDate,
                TimeZone = "Asia/Ho_Chi_Minh",
                EatenAt = new DateTimeOffset(2026, 11, 2, 18, 15, 0, TimeSpan.FromHours(7)),
                TotalCaloriesKcal = 430,
            },
        };

        var snapshot = EatingWindowCalculator.CalculateSnapshot(schedule, localDate, logs, FixedNow);

        Assert.Equal(3, snapshot.CaloricMealCount);
        Assert.Equal(1300, snapshot.TotalCaloriesKcal);
        Assert.Equal(new TimeOnly(7, 30), snapshot.FirstMealLocalTime);
        Assert.Equal(new TimeOnly(18, 15), snapshot.LastMealLocalTime);
        Assert.Equal(645, snapshot.EatingWindowMinutes);
        Assert.True(snapshot.IsWithinTargetWindow);
        Assert.Equal(255, snapshot.MinutesBeforeBedtime);
        Assert.False(snapshot.IsLateEatingFlagged);
        Assert.Equal("OnTrack", snapshot.StatusCode);
    }

    [Fact]
    public void EatingWindow_HandlesUtcToLocalMidnightCrossoverAndPostMidnightSleepTime()
    {
        var userId = Guid.NewGuid();
        // UTC timestamp 2026-11-01 17:30Z is 2026-11-02 00:30 in Asia/Ho_Chi_Minh (UTC+7)
        var utcCrossMidnight = new DateTimeOffset(2026, 11, 1, 17, 30, 0, TimeSpan.Zero);
        var (resolvedDate, resolvedTime) = EatingWindowCalculator.ToUserLocalDateAndTime(
            utcCrossMidnight,
            "Asia/Ho_Chi_Minh");

        Assert.Equal(new DateOnly(2026, 11, 2), resolvedDate);
        Assert.Equal(new TimeOnly(0, 30), resolvedTime);

        // User sleeps past midnight at 00:30 and wakes at 07:30; last meal at 22:15 (135 min before 00:30 sleep)
        var gapMinutes = EatingWindowCalculator.CalculateMinutesBeforeBedtime(
            lastMealLocalTime: new TimeOnly(22, 15),
            wakeTime: new TimeOnly(7, 30),
            sleepTime: new TimeOnly(0, 30));

        Assert.Equal(135, gapMinutes);
    }

    [Fact]
    public async Task MealTracking_RecalculatesEatingWindowAfterEditAndSoftDelete()
    {
        var userId = Guid.NewGuid();
        var userRepo = CreateUserRepositoryWithOnboardedUser(userId);
        var mealLogRepo = new InMemoryMealLogRepository();
        var windowRepo = new InMemoryEatingWindowRepository();
        var scheduleProvider = new TrackingUserScheduleProvider(userRepo);
        var trackingService = new MealTrackingService(
            mealLogRepo,
            windowRepo,
            scheduleProvider,
            new FixedTimeProvider(FixedNow));

        var breakfast = await trackingService.CreateMealLogAsync(
            userId,
            new CreateMealLogCommand(
                new DateTimeOffset(2026, 11, 2, 7, 0, 0, TimeSpan.FromHours(7)),
                "Breakfast",
                "Cháo yến mạch",
                null,
                null,
                [new MealLogItemInput("BF-01", "Cháo yến mạch", 1m, "tô", 350, 11m, 50m, 9m, 8m, true)]));

        var dinner = await trackingService.CreateMealLogAsync(
            userId,
            new CreateMealLogCommand(
                new DateTimeOffset(2026, 11, 2, 21, 30, 0, TimeSpan.FromHours(7)),
                "Dinner",
                "Súp đậu tối muộn",
                null,
                null,
                [new MealLogItemInput("DN-01", "Súp đậu", 1m, "tô", 420, 15m, 55m, 10m, 10m, true)]));

        Assert.True(breakfast.Succeeded);
        Assert.True(dinner.Succeeded);

        var initialWindow = await trackingService.GetEatingWindowSummaryAsync(
            userId,
            new DateOnly(2026, 11, 2));

        // 07:00 to 21:30 = 14h30m (870 min > 720 min) and 21:30 vs 23:00 sleep = 90 min (< 180 min)
        Assert.Equal(870, initialWindow.Value!.EatingWindowMinutes);
        Assert.False(initialWindow.Value.IsWithinTargetWindow);
        Assert.True(initialWindow.Value.IsLateEatingFlagged);

        // Edit dinner to 18:00 local time
        var updatedDinner = await trackingService.UpdateMealLogAsync(
            userId,
            dinner.Value!.Id,
            new UpdateMealLogCommand(
                new DateTimeOffset(2026, 11, 2, 18, 0, 0, TimeSpan.FromHours(7)),
                "Dinner",
                "Súp đậu ăn sớm",
                null,
                "Đã điều chỉnh giờ ăn",
                [new MealLogItemInput("DN-01", "Súp đậu", 1m, "tô", 420, 15m, 55m, 10m, 10m, true)]));

        Assert.True(updatedDinner.Succeeded);

        var windowAfterEdit = await trackingService.GetEatingWindowSummaryAsync(
            userId,
            new DateOnly(2026, 11, 2));

        // 07:00 to 18:00 = 11h00m (660 min <= 720 min) and 18:00 vs 23:00 sleep = 300 min (>= 180 min)
        Assert.Equal(660, windowAfterEdit.Value!.EatingWindowMinutes);
        Assert.True(windowAfterEdit.Value.IsWithinTargetWindow);
        Assert.False(windowAfterEdit.Value.IsLateEatingFlagged);

        // Delete dinner -> only 1 meal remains, window duration becomes 0
        var deleteResult = await trackingService.DeleteMealLogAsync(userId, dinner.Value.Id);
        Assert.True(deleteResult.Succeeded);

        var windowAfterDelete = await trackingService.GetEatingWindowSummaryAsync(
            userId,
            new DateOnly(2026, 11, 2));

        Assert.Equal(1, windowAfterDelete.Value!.CaloricMealCount);
        Assert.Equal(0, windowAfterDelete.Value.EatingWindowMinutes);
        Assert.Equal("SingleMeal", windowAfterDelete.Value.StatusCode);
    }

    private static InMemoryUserRepository CreateUserRepositoryWithOnboardedUser(
        Guid userId,
        int mealFrequency = 3)
    {
        var repo = new InMemoryUserRepository();
        var user = new User
        {
            Id = userId,
            Email = "member3@example.com",
            NormalizedEmail = "MEMBER3@EXAMPLE.COM",
            DisplayName = "Member 3",
            Profile = new UserProfile
            {
                UserId = userId,
                BirthYear = 1995,
                TimeZone = "Asia/Ho_Chi_Minh",
                WakeTime = new TimeOnly(6, 30),
                SleepTime = new TimeOnly(23, 0),
                PreferredMealFrequency = mealFrequency,
                FoodPreference = "Plant-forward Vietnamese",
                ProfileCompleted = true,
            },
        };
        repo.Users.Add(user);
        return repo;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class InMemoryUserRepository : IUserRepository
    {
        public List<User> Users { get; } = [];

        public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
            Task.FromResult(Users.Any(x => x.NormalizedEmail == normalizedEmail));

        public Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
            Task.FromResult(Users.FirstOrDefault(x => x.NormalizedEmail == normalizedEmail));

        public Task<User?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Users.FirstOrDefault(x => x.Id == userId));

        public Task AddAsync(User user, CancellationToken cancellationToken = default)
        {
            Users.Add(user);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class InMemoryMealPlanRepository : IMealPlanRepository
    {
        public List<MealPlan> Plans { get; } = [];
        public Dictionary<Guid, UserDietaryPreference> Preferences { get; } = [];

        public Task<MealPlan?> GetActiveByUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(
                Plans
                    .Where(x => x.UserId == userId && x.Status == MealPlanStatus.Active)
                    .OrderByDescending(x => x.RevisionNumber)
                    .FirstOrDefault());

        public Task<MealPlan?> GetByIdForUserAsync(Guid planId, Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Plans.FirstOrDefault(x => x.Id == planId && x.UserId == userId));

        public Task<IReadOnlyList<MealPlan>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MealPlan>>(
                Plans
                    .Where(x => x.UserId == userId)
                    .OrderByDescending(x => x.RevisionNumber)
                    .ToList());

        public Task<UserDietaryPreference?> GetDietaryPreferenceAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Preferences.GetValueOrDefault(userId));

        public Task UpsertDietaryPreferenceAsync(UserDietaryPreference preference, CancellationToken cancellationToken = default)
        {
            Preferences[preference.UserId] = preference;
            return Task.CompletedTask;
        }

        public Task AddAsync(MealPlan mealPlan, CancellationToken cancellationToken = default)
        {
            Plans.Add(mealPlan);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class InMemoryMealLogRepository : IMealLogRepository
    {
        public List<MealLog> Logs { get; } = [];

        public Task<MealLog?> GetByIdForUserAsync(Guid logId, Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Logs.FirstOrDefault(x => x.Id == logId && x.UserId == userId && !x.IsDeleted));

        public Task<IReadOnlyList<MealLog>> ListByDateRangeAsync(
            Guid userId,
            DateOnly fromDate,
            DateOnly toDate,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MealLog>>(
                Logs
                    .Where(x => x.UserId == userId && !x.IsDeleted && x.LocalDate >= fromDate && x.LocalDate <= toDate)
                    .OrderBy(x => x.LocalDate)
                    .ThenBy(x => x.EatenAt)
                    .ToList());

        public Task<IReadOnlyList<MealLog>> ListActiveByLocalDateAsync(
            Guid userId,
            DateOnly localDate,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MealLog>>(
                Logs
                    .Where(x => x.UserId == userId && !x.IsDeleted && x.LocalDate == localDate)
                    .OrderBy(x => x.EatenAt)
                    .ToList());

        public Task AddAsync(MealLog mealLog, CancellationToken cancellationToken = default)
        {
            Logs.Add(mealLog);
            return Task.CompletedTask;
        }

        public void ReplaceItems(MealLog mealLog, IReadOnlyList<MealLogItem> newItems)
        {
            mealLog.Items.Clear();
            foreach (var item in newItems)
            {
                mealLog.Items.Add(item);
            }
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class InMemoryEatingWindowRepository : IEatingWindowRepository
    {
        public Dictionary<(Guid UserId, DateOnly LocalDate), EatingWindowSnapshot> Snapshots { get; } = [];

        public Task<EatingWindowSnapshot?> GetByUserAndDateAsync(
            Guid userId,
            DateOnly localDate,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Snapshots.GetValueOrDefault((userId, localDate)));

        public Task<IReadOnlyList<EatingWindowSnapshot>> ListByDateRangeAsync(
            Guid userId,
            DateOnly fromDate,
            DateOnly toDate,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EatingWindowSnapshot>>(
                Snapshots.Values
                    .Where(x => x.UserId == userId && x.LocalDate >= fromDate && x.LocalDate <= toDate)
                    .OrderBy(x => x.LocalDate)
                    .ToList());

        public Task UpsertSnapshotAsync(EatingWindowSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            Snapshots[(snapshot.UserId, snapshot.LocalDate)] = snapshot;
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}

