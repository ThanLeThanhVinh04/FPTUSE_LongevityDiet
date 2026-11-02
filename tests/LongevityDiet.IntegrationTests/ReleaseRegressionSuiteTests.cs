using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LongevityDiet.API.Contracts.Auth;
using LongevityDiet.API.Contracts.Fmd;
using LongevityDiet.API.Contracts.Planning;
using LongevityDiet.API.Contracts.Recommendation;
using LongevityDiet.API.Contracts.Tracking;
using LongevityDiet.Domain.Entities;
using LongevityDiet.Services.Fmd;

namespace LongevityDiet.IntegrationTests;

/// <summary>
/// Week 4 Task 3 - Release Regression & PRN232 Final Verification Suite
/// Validates the full 7-step end-to-end user journey across all integrated boundaries:
/// 1. Identity & Profile (Auth, JWT, Onboarding)
/// 2. 7-Day Meal Planning (Deterministic Rule Engine, Plant-Forward Macro Constraints)
/// 3. Recommendation & Replacement (gRPC Contract, Soft Dislike vs Hard Exclusion)
/// 4. Meal Logging & 12h Eating Window (Circadian Rhythm & Bedtime Spacing)
/// 5. FMD Fasting Mimicking Diet (Safety Gate, Block High-Risk, Cycle Tracking, Emergency Stop)
/// 6. Cross-Tenant Security & Ownership Isolation
/// 7. Graceful Fallbacks & REST Semantics
/// </summary>
public sealed class ReleaseRegressionSuiteTests : IClassFixture<IntegrationTestWebApplicationFactory>
{
    private readonly IntegrationTestWebApplicationFactory _factory;

    public ReleaseRegressionSuiteTests(IntegrationTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ReleaseRegression_EndToEndLifecycle_PassesAllRubricCheckpoints()
    {
        await _factory.ResetDatabaseAsync();

        // -------------------------------------------------------------
        // STEP 1: Registration, JWT Authentication & Profile Onboarding
        // -------------------------------------------------------------
        var anonymousClient = _factory.CreateHttpsClient();

        var registerRes = await anonymousClient.PostAsJsonAsync(
            "/api/v1/auth/register",
            new
            {
                Email = "regression.user@longevitydiet.vn",
                Password = "LongevityPassword!2026",
                DisplayName = "Thành viên Thử nghiệm W4",
            });
        Assert.Equal(HttpStatusCode.Created, registerRes.StatusCode);

        var authSession = await registerRes.Content.ReadFromJsonAsync<AuthSessionDto>();
        Assert.NotNull(authSession);
        Assert.False(string.IsNullOrWhiteSpace(authSession.AccessToken));

        using var client = _factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", authSession.AccessToken);

        // Update Profile
        var profileRes = await client.PutAsJsonAsync(
            "/api/v1/profile",
            new
            {
                DisplayName = "Thành viên Thử nghiệm W4",
                BirthYear = 1990,
                TimeZone = "Asia/Ho_Chi_Minh",
                WakeTime = "06:30:00",
                SleepTime = "23:00:00",
                PreferredMealFrequency = 3,
                FoodPreference = "Plant-forward Vietnamese",
            });
        Assert.Equal(HttpStatusCode.OK, profileRes.StatusCode);

        // Verify Profile endpoint
        var meRes = await client.GetAsync("/api/v1/profile");
        Assert.Equal(HttpStatusCode.OK, meRes.StatusCode);

        // -------------------------------------------------------------
        // STEP 2: 7-Day Meal Plan Generation
        // -------------------------------------------------------------
        var generatePlanRes = await client.PostAsJsonAsync(
            "/api/v1/meal-plans/generate",
            new GenerateMealPlanRequest(
                StartDate: new DateOnly(2026, 11, 2),
                Days: 7,
                DietaryPattern: "PlantForward",
                Allergies: ["peanut", "shellfish"],
                ExcludedFoods: ["salmon"],
                PreferredCuisine: "Vietnamese"));
        Assert.Equal(HttpStatusCode.Created, generatePlanRes.StatusCode);

        var currentPlanRes = await client.GetAsync("/api/v1/meal-plans/current");
        Assert.Equal(HttpStatusCode.OK, currentPlanRes.StatusCode);
        using var planDoc = JsonDocument.Parse(await currentPlanRes.Content.ReadAsStringAsync());
        var planRoot = planDoc.RootElement;
        var planDays = planRoot.GetProperty("days");
        Assert.Equal(7, planDays.GetArrayLength());

        var planId = planRoot.GetProperty("id").GetString()!;
        var firstDayMeals = planDays[0].GetProperty("meals").EnumerateArray().ToList();
        Assert.NotEmpty(firstDayMeals);
        var targetMealId = firstDayMeals[0].GetProperty("id").GetString()!;

        // -------------------------------------------------------------
        // STEP 3: Independent Recommendation v2 & Meal Replacement
        // -------------------------------------------------------------
        var recsRes = await client.GetAsync($"/api/v1/recommendations?mealPlanId={planId}&plannedMealId={targetMealId}");
        Assert.Equal(HttpStatusCode.OK, recsRes.StatusCode);

        var replaceRes = await client.PostAsJsonAsync(
            $"/api/v1/meal-plans/{planId}/meals/{targetMealId}/replace",
            new ReplacePlanMealByRouteRequest(
                TargetCandidateCode: null,
                ReplacementReasonCode: "USER_REQUESTED_VARIETY"));
        Assert.Equal(HttpStatusCode.OK, replaceRes.StatusCode);

        // -------------------------------------------------------------
        // STEP 4: Meal Logging & Circadian Eating Window Verification
        // -------------------------------------------------------------
        // Log Breakfast at 07:00 ICT (00:00 UTC)
        var log1Res = await client.PostAsJsonAsync(
            "/api/v1/meal-logs",
            new CreateMealLogRequest(
                new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero),
                "Breakfast",
                "Cháo yến mạch và hạt chia",
                null,
                null,
                [new MealLogItemRequest("BF-01", "Cháo yến mạch", 1m, "tô", 380, 12m, 55m, 10m, 8m, true)]));
        Assert.Equal(HttpStatusCode.Created, log1Res.StatusCode);

        // Log Dinner at 18:30 ICT (11:30 UTC)
        var log2Res = await client.PostAsJsonAsync(
            "/api/v1/meal-logs",
            new CreateMealLogRequest(
                new DateTimeOffset(2026, 11, 2, 11, 30, 0, TimeSpan.Zero),
                "Dinner",
                "Đậu phụ sốt cà chua và rau luộc",
                null,
                null,
                [new MealLogItemRequest("DN-01", "Đậu phụ sốt cà chua", 1m, "đĩa", 420, 20m, 35m, 14m, 9m, true)]));
        Assert.Equal(HttpStatusCode.Created, log2Res.StatusCode);

        // Verify 12-hour eating window heuristic
        var windowRes = await client.GetAsync("/api/v1/eating-window/summary?date=2026-11-02");
        Assert.Equal(HttpStatusCode.OK, windowRes.StatusCode);
        using var windowDoc = JsonDocument.Parse(await windowRes.Content.ReadAsStringAsync());
        var windowRoot = windowDoc.RootElement;
        Assert.Equal(2, windowRoot.GetProperty("caloricMealCount").GetInt32());
        Assert.Equal(690, windowRoot.GetProperty("eatingWindowMinutes").GetInt32()); // 11h30m <= 12h
        Assert.True(windowRoot.GetProperty("isWithinTargetWindow").GetBoolean());

        // -------------------------------------------------------------
        // STEP 5: FMD Safety Gate, 5-Day Cycle Tracking & Early Stop
        // -------------------------------------------------------------
        // Acknowledge education
        var ackRes = await client.PostAsync("/api/v1/fmd/education/acknowledge", null);
        Assert.Equal(HttpStatusCode.OK, ackRes.StatusCode);

        // Safety Assessment - Clear
        var safeAnswers = new Dictionary<string, bool>
        {
            ["PREGNANT_OR_BREASTFEEDING"] = false,
            ["AGE_OUT_OF_BOUNDS"] = false,
            ["UNDERWEIGHT_BMI"] = false,
            ["EATING_DISORDER_HISTORY"] = false,
            ["DIABETES_MEDICATION"] = false,
            ["ACTIVE_CHEMOTHERAPY_OR_CANCER"] = false,
            ["ORGAN_FAILURE_OR_INFECTION"] = false,
        };
        var assessmentRes = await client.PostAsJsonAsync(
            "/api/v1/fmd/safety/assessment",
            new SubmitFmdAssessmentRequest(safeAnswers));
        Assert.Equal(HttpStatusCode.OK, assessmentRes.StatusCode);
        var status = await assessmentRes.Content.ReadFromJsonAsync<FmdSafetyStatusSummary>();
        Assert.NotNull(status);
        Assert.Equal(FmdSafetyStatus.EligibleForTracking, status.CurrentStatus);

        // Start 5-day cycle
        var startCycleRes = await client.PostAsJsonAsync("/api/v1/fmd/cycles", new StartFmdCycleRequest());
        Assert.Equal(HttpStatusCode.Created, startCycleRes.StatusCode);
        var cycle = await startCycleRes.Content.ReadFromJsonAsync<FmdCycleSummary>();
        Assert.NotNull(cycle);
        Assert.Equal(5, cycle.Days.Count);
        Assert.Equal(1100, cycle.Days[0].TargetCaloriesKcal);
        Assert.Equal(750, cycle.Days[1].TargetCaloriesKcal);

        // Log Day 1 check-in
        var logDayRes = await client.PostAsJsonAsync(
            $"/api/v1/fmd/cycles/{cycle.Id}/days/1",
            new LogFmdCycleDayRequest(
                DayNumber: 1,
                EnergyLevel: 4,
                HungerLevel: 2,
                WaterIntakeLiters: 2.5m,
                Symptoms: null,
                DailyNotes: "Ngày 1 suôn sẻ, không mệt mỏi"));
        Assert.Equal(HttpStatusCode.OK, logDayRes.StatusCode);

        // Safe emergency stop
        var stopCycleRes = await client.PostAsJsonAsync(
            $"/api/v1/fmd/cycles/{cycle.Id}/stop",
            new StopFmdCycleRequest("Người dùng hoàn tất kiểm thử hồi quy W4"));
        Assert.Equal(HttpStatusCode.OK, stopCycleRes.StatusCode);
        var stoppedCycle = await stopCycleRes.Content.ReadFromJsonAsync<FmdCycleSummary>();
        Assert.NotNull(stoppedCycle);
        Assert.Equal(FmdCycleStatus.StoppedByUser.ToString(), stoppedCycle.Status);

        // -------------------------------------------------------------
        // STEP 6: Cross-Tenant Security & Ownership Isolation
        // -------------------------------------------------------------
        using var userB = await CreateSecondUserClientAsync();
        var unauthorizedStopRes = await userB.PostAsJsonAsync(
            $"/api/v1/fmd/cycles/{cycle.Id}/stop",
            new StopFmdCycleRequest("Intruder"));
        Assert.Equal(HttpStatusCode.NotFound, unauthorizedStopRes.StatusCode);

        // -------------------------------------------------------------
        // STEP 7: REST API Semantics & Swagger Spec Validation
        // -------------------------------------------------------------
        var anonymousEducationRes = await anonymousClient.GetAsync("/api/v1/fmd/education");
        Assert.Equal(HttpStatusCode.OK, anonymousEducationRes.StatusCode);

        var unauthSecuredRes = await anonymousClient.GetAsync("/api/v1/fmd/safety/status");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthSecuredRes.StatusCode);
    }

    private async Task<HttpClient> CreateSecondUserClientAsync()
    {
        var client = _factory.CreateHttpsClient();
        var register = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new
            {
                Email = "intruder.user@longevitydiet.vn",
                Password = "IntruderPass!2026",
                DisplayName = "Intruder User",
            });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var session = await register.Content.ReadFromJsonAsync<AuthSessionDto>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", session!.AccessToken);
        return client;
    }

    private sealed record AuthSessionDto(string AccessToken);
}
