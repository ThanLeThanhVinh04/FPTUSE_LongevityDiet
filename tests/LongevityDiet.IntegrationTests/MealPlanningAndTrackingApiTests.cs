using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LongevityDiet.API.Contracts.Planning;
using LongevityDiet.API.Contracts.Tracking;

namespace LongevityDiet.IntegrationTests;

public sealed class MealPlanningAndTrackingApiTests : IClassFixture<IntegrationTestWebApplicationFactory>
{
    private readonly IntegrationTestWebApplicationFactory _factory;

    public MealPlanningAndTrackingApiTests(IntegrationTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task MealPlanEndpoints_GenerateGetRegenerateMealAndRegeneratePlan_EnforceAllergiesAndExclusions()
    {
        await _factory.ResetDatabaseAsync();
        using var client = await CreateOnboardedUserClientAsync("planner-user");

        var generateResponse = await client.PostAsJsonAsync(
            "/api/v1/meal-plans/generate",
            new GenerateMealPlanRequest(
                StartDate: new DateOnly(2026, 11, 2),
                Days: 7,
                DietaryPattern: "PlantForward",
                Allergies: ["peanut", "shellfish"],
                ExcludedFoods: ["salmon", "tofu"],
                PreferredCuisine: "Vietnamese"));

        Assert.Equal(HttpStatusCode.Created, generateResponse.StatusCode);
        var plan = await generateResponse.Content.ReadFromJsonAsync<MealPlanResponse>();
        Assert.NotNull(plan);
        Assert.Equal(7, plan.Days.Count);
        Assert.Equal(1, plan.RevisionNumber);
        Assert.Contains("peanut", plan.AppliedAllergies);
        Assert.Contains("salmon", plan.AppliedExclusions);

        foreach (var meal in plan.Days.SelectMany(d => d.Meals))
        {
            Assert.DoesNotContain("peanut", meal.Allergens, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain("shellfish", meal.Allergens, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain("salmon", meal.Ingredients, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain("tofu", meal.Ingredients, StringComparer.OrdinalIgnoreCase);
        }

        // Get current plan
        var currentResponse = await client.GetAsync("/api/v1/meal-plans/current");
        Assert.Equal(HttpStatusCode.OK, currentResponse.StatusCode);

        // Regenerate first planned meal on Day 1
        var firstMeal = plan.Days[0].Meals[0];
        var regenerateMealResponse = await client.PostAsJsonAsync(
            $"/api/v1/meal-plans/{plan.Id}/meals/{firstMeal.Id}/regenerate",
            new RegeneratePlannedMealRequest());

        Assert.Equal(HttpStatusCode.OK, regenerateMealResponse.StatusCode);
        var updatedPlan = await regenerateMealResponse.Content.ReadFromJsonAsync<MealPlanResponse>();
        Assert.NotNull(updatedPlan);
        var regeneratedMeal = updatedPlan.Days[0].Meals[0];
        Assert.Equal(1, regeneratedMeal.RegenerationCount);
        Assert.NotEqual(firstMeal.CandidateCode, regeneratedMeal.CandidateCode);

        // Regenerate entire plan -> increments revision and preserves previous revision
        var regeneratePlanResponse = await client.PostAsJsonAsync(
            $"/api/v1/meal-plans/{plan.Id}/regenerate",
            new RegenerateMealPlanRequest());

        Assert.Equal(HttpStatusCode.OK, regeneratePlanResponse.StatusCode);
        var revision2 = await regeneratePlanResponse.Content.ReadFromJsonAsync<MealPlanResponse>();
        Assert.NotNull(revision2);
        Assert.Equal(2, revision2.RevisionNumber);
        Assert.Equal(plan.Id, revision2.PreviousPlanId);
    }

    [Fact]
    public async Task MealPlanAndMealLogEndpoints_EnforceUserIdOwnership_PreventCrossUserAccess()
    {
        await _factory.ResetDatabaseAsync();
        using var userAClient = await CreateOnboardedUserClientAsync("owner-a");
        using var userBClient = await CreateOnboardedUserClientAsync("owner-b");

        var createPlanResponse = await userAClient.PostAsJsonAsync(
            "/api/v1/meal-plans/generate",
            new GenerateMealPlanRequest(new DateOnly(2026, 11, 2), Days: 7));
        Assert.Equal(HttpStatusCode.Created, createPlanResponse.StatusCode);
        var userAPlan = (await createPlanResponse.Content.ReadFromJsonAsync<MealPlanResponse>())!;

        var createLogResponse = await userAClient.PostAsJsonAsync(
            "/api/v1/meal-logs",
            new CreateMealLogRequest(
                new DateTimeOffset(2026, 11, 2, 7, 30, 0, TimeSpan.FromHours(7)),
                "Breakfast",
                "Cháo gạo lứt đậu xanh",
                userAPlan.Days[0].Meals[0].Id,
                "Bữa sáng của User A",
                [new MealLogItemRequest("BF-02", "Cháo gạo lứt đậu xanh", 1m, "tô", 340, 12.5m, 58m, 6m, 8.5m, true)]));
        Assert.Equal(HttpStatusCode.Created, createLogResponse.StatusCode);
        var userALog = (await createLogResponse.Content.ReadFromJsonAsync<MealLogResponse>())!;

        // User B attempts to read or regenerate User A's plan -> 404 NotFound
        var getPlanByB = await userBClient.GetAsync($"/api/v1/meal-plans/{userAPlan.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getPlanByB.StatusCode);

        var regenPlanByB = await userBClient.PostAsJsonAsync(
            $"/api/v1/meal-plans/{userAPlan.Id}/regenerate",
            new RegenerateMealPlanRequest());
        Assert.Equal(HttpStatusCode.NotFound, regenPlanByB.StatusCode);

        // User B attempts to update or delete User A's MealLog -> 404 NotFound
        var updateLogByB = await userBClient.PutAsJsonAsync(
            $"/api/v1/meal-logs/{userALog.Id}",
            new UpdateMealLogRequest(
                new DateTimeOffset(2026, 11, 2, 8, 0, 0, TimeSpan.FromHours(7)),
                "Breakfast",
                "Hack attempt",
                null,
                null,
                [new MealLogItemRequest(null, "Hack", 1m, "phần", 100, 1m, 1m, 1m, 1m, true)]));
        Assert.Equal(HttpStatusCode.NotFound, updateLogByB.StatusCode);

        var deleteLogByB = await userBClient.DeleteAsync($"/api/v1/meal-logs/{userALog.Id}");
        Assert.Equal(HttpStatusCode.NotFound, deleteLogByB.StatusCode);

        // User B's own meal log list is empty
        var userBLogs = await userBClient.GetFromJsonAsync<List<MealLogResponse>>(
            "/api/v1/meal-logs?fromDate=2026-11-01&toDate=2026-11-05");
        Assert.NotNull(userBLogs);
        Assert.Empty(userBLogs);
    }

    [Fact]
    public async Task MealLogAndEatingWindowEndpoints_CreateUpdateDeleteAndDateRange_RecalculateWindowAndHandleTimezone()
    {
        await _factory.ResetDatabaseAsync();
        using var client = await CreateOnboardedUserClientAsync("tracker-user");

        // Log Breakfast at 07:30 ICT (00:30 UTC) and Dinner at 18:30 ICT (11:30 UTC) on 2026-11-02
        var breakfastRes = await client.PostAsJsonAsync(
            "/api/v1/meal-logs",
            new CreateMealLogRequest(
                new DateTimeOffset(2026, 11, 2, 0, 30, 0, TimeSpan.Zero),
                "Breakfast",
                "Khoai lang hấp và đậu đen",
                null,
                null,
                [new MealLogItemRequest("BF-03", "Khoai lang hấp", 1m, "đĩa", 355, 11.8m, 54m, 10.5m, 10m, true)]));
        Assert.Equal(HttpStatusCode.Created, breakfastRes.StatusCode);

        var dinnerRes = await client.PostAsJsonAsync(
            "/api/v1/meal-logs",
            new CreateMealLogRequest(
                new DateTimeOffset(2026, 11, 2, 11, 30, 0, TimeSpan.Zero),
                "Dinner",
                "Canh bí đỏ đậu xanh",
                null,
                null,
                [new MealLogItemRequest("DN-02", "Canh bí đỏ đậu xanh", 1m, "suất", 420, 14.8m, 64m, 10m, 10.5m, true)]));
        Assert.Equal(HttpStatusCode.Created, dinnerRes.StatusCode);
        var dinnerLog = (await dinnerRes.Content.ReadFromJsonAsync<MealLogResponse>())!;

        var summary = await client.GetFromJsonAsync<EatingWindowSummaryResponse>(
            "/api/v1/eating-window/summary?date=2026-11-02");
        Assert.NotNull(summary);
        Assert.Equal(2, summary.CaloricMealCount);
        Assert.Equal(775, summary.TotalCaloriesKcal);
        Assert.Equal(new TimeOnly(7, 30), summary.FirstMealLocalTime);
        Assert.Equal(new TimeOnly(18, 30), summary.LastMealLocalTime);
        Assert.Equal(660, summary.EatingWindowMinutes); // 11 hours
        Assert.True(summary.IsWithinTargetWindow);
        Assert.Equal(270, summary.MinutesBeforeBedtime); // 18:30 to 23:00 = 4h30m
        Assert.False(summary.IsLateEatingFlagged);

        // Delete dinner and verify recalculation
        var deleteRes = await client.DeleteAsync($"/api/v1/meal-logs/{dinnerLog.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteRes.StatusCode);

        var summaryAfterDelete = await client.GetFromJsonAsync<EatingWindowSummaryResponse>(
            "/api/v1/eating-window/summary?date=2026-11-02");
        Assert.NotNull(summaryAfterDelete);
        Assert.Equal(1, summaryAfterDelete.CaloricMealCount);
        Assert.Equal(355, summaryAfterDelete.TotalCaloriesKcal);
        Assert.Equal(0, summaryAfterDelete.EatingWindowMinutes);
    }

    private async Task<HttpClient> CreateOnboardedUserClientAsync(string prefix)
    {
        var client = _factory.CreateHttpsClient();
        var suffix = Guid.NewGuid().ToString("N");

        var register = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new
            {
                Email = $"{prefix}-{suffix}@example.test",
                Password = "StrongPass123!",
                DisplayName = $"Member {prefix}",
            });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var session = await register.Content.ReadFromJsonAsync<AuthSessionDto>();
        Assert.NotNull(session);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", session.AccessToken);

        var profileUpdate = await client.PutAsJsonAsync(
            "/api/v1/profile",
            new
            {
                DisplayName = $"Member {prefix}",
                BirthYear = 1996,
                TimeZone = "Asia/Ho_Chi_Minh",
                WakeTime = "06:30:00",
                SleepTime = "23:00:00",
                PreferredMealFrequency = 3,
                FoodPreference = "Plant-forward Vietnamese",
            });
        Assert.Equal(HttpStatusCode.OK, profileUpdate.StatusCode);

        return client;
    }

    private sealed record AuthSessionDto(string AccessToken);
}

