using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LongevityDiet.API.Contracts.Fmd;
using LongevityDiet.Domain.Entities;
using LongevityDiet.Services.Fmd;

namespace LongevityDiet.IntegrationTests;

public sealed class FmdApiTests : IClassFixture<IntegrationTestWebApplicationFactory>
{
    private readonly IntegrationTestWebApplicationFactory _factory;

    public FmdApiTests(IntegrationTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Fmd_GetEducation_ReturnsAnonymousAccessibleEducationalContent()
    {
        var client = _factory.CreateHttpsClient();

        var response = await client.GetAsync("/api/v1/fmd/education");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var education = doc.RootElement.GetProperty("education");
        var questions = doc.RootElement.GetProperty("screeningQuestions");

        Assert.True(education.GetProperty("keyPrinciples").GetArrayLength() > 0);
        Assert.True(questions.GetArrayLength() >= 7);
        Assert.Contains("KHÔNG PHẢI là thiết bị y tế", education.GetProperty("scopeDisclaimer").GetString());
    }

    [Fact]
    public async Task Fmd_SafetyWorkflow_EnforcesStateTransitionsAndBlocksHighRiskUsers()
    {
        await _factory.ResetDatabaseAsync();
        using var client = await CreateOnboardedUserClientAsync("fmd-safety");

        // 1. Initial status should be EducationNotViewed
        var statusRes1 = await client.GetFromJsonAsync<FmdSafetyStatusSummary>("/api/v1/fmd/safety/status");
        Assert.NotNull(statusRes1);
        Assert.Equal(FmdSafetyStatus.EducationNotViewed, statusRes1.CurrentStatus);
        Assert.False(statusRes1.IsEligibleForTracking);

        // 2. Cannot start cycle before acknowledgement and screening -> 400 BadRequest
        var invalidStartRes = await client.PostAsJsonAsync("/api/v1/fmd/cycles", new StartFmdCycleRequest());
        Assert.Equal(HttpStatusCode.BadRequest, invalidStartRes.StatusCode);

        // 3. Acknowledge education -> transitions to AssessmentRequired
        var ackRes = await client.PostAsync("/api/v1/fmd/education/acknowledge", null);
        Assert.Equal(HttpStatusCode.OK, ackRes.StatusCode);

        var statusRes2 = await client.GetFromJsonAsync<FmdSafetyStatusSummary>("/api/v1/fmd/safety/status");
        Assert.NotNull(statusRes2);
        Assert.Equal(FmdSafetyStatus.AssessmentRequired, statusRes2.CurrentStatus);

        // 4. Submit high-risk assessment (Diabetes medication) -> ProfessionalReviewRequired
        var highRiskAnswers = new Dictionary<string, bool>
        {
            ["PREGNANT_OR_BREASTFEEDING"] = false,
            ["AGE_OUT_OF_BOUNDS"] = false,
            ["UNDERWEIGHT_BMI"] = false,
            ["EATING_DISORDER_HISTORY"] = false,
            ["DIABETES_MEDICATION"] = true, // High risk!
            ["ACTIVE_CHEMOTHERAPY_OR_CANCER"] = false,
            ["ORGAN_FAILURE_OR_INFECTION"] = false,
        };

        var highRiskRes = await client.PostAsJsonAsync(
            "/api/v1/fmd/safety/assessment",
            new SubmitFmdAssessmentRequest(highRiskAnswers, "Bác sĩ đang kê đơn Metformin"));
        Assert.Equal(HttpStatusCode.OK, highRiskRes.StatusCode);

        var highRiskSummary = await highRiskRes.Content.ReadFromJsonAsync<FmdSafetyStatusSummary>();
        Assert.NotNull(highRiskSummary);
        Assert.Equal(FmdSafetyStatus.ProfessionalReviewRequired, highRiskSummary.CurrentStatus);
        Assert.False(highRiskSummary.IsEligibleForTracking);
        Assert.True(highRiskSummary.RequiresProfessionalReview);
        Assert.Contains("DIABETES_MEDICATION", highRiskSummary.TriggeredRiskCodes);

        // 5. High-risk user attempts to start cycle -> blocked by backend with 422
        var blockedStartRes = await client.PostAsJsonAsync("/api/v1/fmd/cycles", new StartFmdCycleRequest());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, blockedStartRes.StatusCode);

        // 6. Submit safe assessment (all negative) -> EligibleForTracking
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

        var safeRes = await client.PostAsJsonAsync(
            "/api/v1/fmd/safety/assessment",
            new SubmitFmdAssessmentRequest(safeAnswers));
        Assert.Equal(HttpStatusCode.OK, safeRes.StatusCode);

        var safeSummary = await safeRes.Content.ReadFromJsonAsync<FmdSafetyStatusSummary>();
        Assert.NotNull(safeSummary);
        Assert.Equal(FmdSafetyStatus.EligibleForTracking, safeSummary.CurrentStatus);
        Assert.True(safeSummary.IsEligibleForTracking);
        Assert.False(safeSummary.RequiresProfessionalReview);
    }

    [Fact]
    public async Task Fmd_CycleLifecycle_StartsLogsAndStopsCycleCorrectly()
    {
        await _factory.ResetDatabaseAsync();
        using var client = await CreateOnboardedUserClientAsync("fmd-cycle");

        // Prepare eligibility
        await client.PostAsync("/api/v1/fmd/education/acknowledge", null);
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
        await client.PostAsJsonAsync("/api/v1/fmd/safety/assessment", new SubmitFmdAssessmentRequest(safeAnswers));

        // Start cycle
        var startCycleRes = await client.PostAsJsonAsync(
            "/api/v1/fmd/cycles",
            new StartFmdCycleRequest(Notes: "Bắt đầu chu kỳ FMD tháng 11"));
        Assert.Equal(HttpStatusCode.Created, startCycleRes.StatusCode);

        var cycle = await startCycleRes.Content.ReadFromJsonAsync<FmdCycleSummary>();
        Assert.NotNull(cycle);
        Assert.Equal(FmdCycleStatus.Active.ToString(), cycle.Status);
        Assert.Equal(5, cycle.Days.Count);
        Assert.Equal(1100, cycle.Days[0].TargetCaloriesKcal);
        Assert.Equal(750, cycle.Days[1].TargetCaloriesKcal);

        // Attempt duplicate active cycle -> 409 Conflict
        var dupCycleRes = await client.PostAsJsonAsync("/api/v1/fmd/cycles", new StartFmdCycleRequest());
        Assert.Equal(HttpStatusCode.Conflict, dupCycleRes.StatusCode);

        // Log Day 1 check-in
        var logDayRes = await client.PostAsJsonAsync(
            $"/api/v1/fmd/cycles/{cycle.Id}/days/1",
            new LogFmdCycleDayRequest(
                DayNumber: 1,
                EnergyLevel: 4,
                HungerLevel: 3,
                WaterIntakeLiters: 2.2m,
                Symptoms: null,
                DailyNotes: "Cảm thấy nhẹ nhõm, không bị chóng mặt"));
        Assert.Equal(HttpStatusCode.OK, logDayRes.StatusCode);

        var updatedCycle = await logDayRes.Content.ReadFromJsonAsync<FmdCycleSummary>();
        Assert.NotNull(updatedCycle);
        var day1 = updatedCycle.Days.First(d => d.DayNumber == 1);
        Assert.True(day1.IsCompleted);
        Assert.Equal(4, day1.EnergyLevel);
        Assert.Equal(2.2m, day1.WaterIntakeLiters);

        // Stop cycle anytime
        var stopRes = await client.PostAsJsonAsync(
            $"/api/v1/fmd/cycles/{cycle.Id}/stop",
            new StopFmdCycleRequest("Cảm thấy mệt mỏi cần dừng sớm để đảm bảo sức khỏe"));
        Assert.Equal(HttpStatusCode.OK, stopRes.StatusCode);

        var stoppedCycle = await stopRes.Content.ReadFromJsonAsync<FmdCycleSummary>();
        Assert.NotNull(stoppedCycle);
        Assert.Equal(FmdCycleStatus.StoppedByUser.ToString(), stoppedCycle.Status);
        Assert.NotNull(stoppedCycle.StoppedAt);
        Assert.Contains("mệt mỏi", stoppedCycle.StopReason);

        // Active cycle endpoint should now return 404 NotFound
        var activeRes = await client.GetAsync("/api/v1/fmd/cycles/active");
        Assert.Equal(HttpStatusCode.NotFound, activeRes.StatusCode);

        // List cycles should contain the stopped cycle
        var listRes = await client.GetFromJsonAsync<IReadOnlyList<FmdCycleSummary>>("/api/v1/fmd/cycles");
        Assert.NotNull(listRes);
        Assert.Single(listRes);
        Assert.Equal(cycle.Id, listRes[0].Id);
    }

    [Fact]
    public async Task Fmd_Cycle_EnforcesUserOwnershipIsolation()
    {
        await _factory.ResetDatabaseAsync();
        using var userAClient = await CreateOnboardedUserClientAsync("user-a");
        using var userBClient = await CreateOnboardedUserClientAsync("user-b");

        // User A sets up and starts cycle
        await userAClient.PostAsync("/api/v1/fmd/education/acknowledge", null);
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
        await userAClient.PostAsJsonAsync("/api/v1/fmd/safety/assessment", new SubmitFmdAssessmentRequest(safeAnswers));
        var startCycleRes = await userAClient.PostAsJsonAsync("/api/v1/fmd/cycles", new StartFmdCycleRequest());
        var userACycle = (await startCycleRes.Content.ReadFromJsonAsync<FmdCycleSummary>())!;

        // User B tries to log day on User A's cycle -> 404 NotFound
        var intruderLogRes = await userBClient.PostAsJsonAsync(
            $"/api/v1/fmd/cycles/{userACycle.Id}/days/1",
            new LogFmdCycleDayRequest(
                DayNumber: 1,
                EnergyLevel: 5,
                HungerLevel: 2,
                WaterIntakeLiters: 2.5m,
                Symptoms: null,
                DailyNotes: "Intruder note"));
        Assert.Equal(HttpStatusCode.NotFound, intruderLogRes.StatusCode);

        // User B tries to stop User A's cycle -> 404 NotFound
        var intruderStopRes = await userBClient.PostAsJsonAsync(
            $"/api/v1/fmd/cycles/{userACycle.Id}/stop",
            new StopFmdCycleRequest("Intruder stop"));
        Assert.Equal(HttpStatusCode.NotFound, intruderStopRes.StatusCode);
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
