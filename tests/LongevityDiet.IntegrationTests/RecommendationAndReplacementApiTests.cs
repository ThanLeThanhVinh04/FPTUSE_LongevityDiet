using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LongevityDiet.API.Contracts.Auth;
using LongevityDiet.API.Contracts.Planning;
using LongevityDiet.API.Contracts.Recommendation;

namespace LongevityDiet.IntegrationTests;

public sealed class RecommendationAndReplacementApiTests : IClassFixture<IntegrationTestWebApplicationFactory>
{
    private readonly IntegrationTestWebApplicationFactory _factory;

    public RecommendationAndReplacementApiTests(IntegrationTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Recommendations_FeedbackAndPreferences_DistinguishDislikeFromExplicitExclusion()
    {
        await _factory.ResetDatabaseAsync();
        using var client = await CreateAuthenticatedClientAsync("w2-rec-feedback@example.com");

        // 1. Get initial Lunch recommendations
        var initialRecsResponse = await client.GetAsync("/api/v1/recommendations?slotType=Lunch");
        Assert.Equal(HttpStatusCode.OK, initialRecsResponse.StatusCode);
        using var initialDoc = JsonDocument.Parse(await initialRecsResponse.Content.ReadAsStringAsync());
        var acceptedInitial = initialDoc.RootElement.GetProperty("acceptedCandidates");
        Assert.True(acceptedInitial.GetArrayLength() >= 2);

        var topCandidateCode = acceptedInitial[0].GetProperty("candidateCode").GetString()!;

        // 2. Submit Dislike feedback for the top candidate -> candidate remains in acceptedCandidates with SOFT_DISLIKE_PENALTY
        var dislikeResponse = await client.PostAsJsonAsync(
            "/api/v1/recommendations/feedback",
            new SubmitRecommendationFeedbackRequest(
                CandidateCode: topCandidateCode,
                FeedbackType: "Dislike",
                ReasonCode: "TASTE_PREFERENCE",
                Notes: "Hôm nay muốn đổi vị khác"));
        Assert.Equal(HttpStatusCode.OK, dislikeResponse.StatusCode);

        var afterDislikeResponse = await client.GetAsync("/api/v1/recommendations?slotType=Lunch");
        Assert.Equal(HttpStatusCode.OK, afterDislikeResponse.StatusCode);
        using var dislikeDoc = JsonDocument.Parse(await afterDislikeResponse.Content.ReadAsStringAsync());
        var acceptedAfterDislike = dislikeDoc.RootElement.GetProperty("acceptedCandidates").EnumerateArray().ToList();

        var dislikedMatch = acceptedAfterDislike.FirstOrDefault(
            x => x.GetProperty("candidateCode").GetString() == topCandidateCode);
        Assert.NotEqual(default, dislikedMatch);
        Assert.True(dislikedMatch.GetProperty("isDisliked").GetBoolean());
        Assert.Contains(
            dislikedMatch.GetProperty("reasonCodes").EnumerateArray().Select(x => x.GetString()),
            code => code == "SOFT_DISLIKE_PENALTY");

        // 3. Add ExplicitExclusion for the same candidate -> candidate is now hard-rejected in rejectedCandidates
        var exclusionResponse = await client.PostAsJsonAsync(
            "/api/v1/recommendations/preferences",
            new UpsertFoodPreferenceRequest(
                FoodToken: topCandidateCode,
                PreferenceKind: "ExplicitExclusion",
                DisplayLabel: "Loại trừ hoàn toàn món này",
                Notes: "Không dùng món này"));
        Assert.Equal(HttpStatusCode.OK, exclusionResponse.StatusCode);

        var afterExclusionResponse = await client.GetAsync("/api/v1/recommendations?slotType=Lunch");
        Assert.Equal(HttpStatusCode.OK, afterExclusionResponse.StatusCode);
        using var exclusionDoc = JsonDocument.Parse(await afterExclusionResponse.Content.ReadAsStringAsync());

        var acceptedAfterExclusion = exclusionDoc.RootElement
            .GetProperty("acceptedCandidates")
            .EnumerateArray()
            .Select(x => x.GetProperty("candidateCode").GetString())
            .ToList();
        Assert.DoesNotContain(topCandidateCode, acceptedAfterExclusion);

        var rejectedAfterExclusion = exclusionDoc.RootElement
            .GetProperty("rejectedCandidates")
            .EnumerateArray()
            .ToList();
        Assert.Contains(
            rejectedAfterExclusion,
            r => r.GetProperty("candidateCode").GetString() == topCandidateCode);
    }

    [Fact]
    public async Task ReplacePlannedMeal_EnforcesHardConstraints_UpdatesAtomically_AndRecordsAuditHistory()
    {
        await _factory.ResetDatabaseAsync();
        using var client = await CreateAuthenticatedClientAsync("w2-replace-audit@example.com");

        // 1. Generate a 7-day plan with fish & shellfish allergy
        var generateResponse = await client.PostAsJsonAsync(
            "/api/v1/meal-plans/generate",
            new GenerateMealPlanRequest(
                StartDate: new DateOnly(2026, 11, 2),
                Days: 7,
                DietaryPattern: "PlantForward",
                Allergies: ["fish", "shellfish"],
                ExcludedFoods: ["mushroom"],
                PreferredCuisine: "Vietnamese"));

        Assert.Equal(HttpStatusCode.Created, generateResponse.StatusCode);
        var plan = await generateResponse.Content.ReadFromJsonAsync<MealPlanResponse>();
        Assert.NotNull(plan);

        var targetDay = plan.Days[0];
        var targetMeal = targetDay.Meals.First(m => m.SlotType == "Lunch");

        // 2. Attempting to replace with a fish-containing candidate must fail with 422 UnprocessableEntity
        var unsafeReplaceResponse = await client.PostAsJsonAsync(
            $"/api/v1/meal-plans/{plan.Id}/meals/{targetMeal.Id}/replace",
            new ReplacePlanMealByRouteRequest(
                TargetCandidateCode: "LN-MACKEREL-GREENS-03",
                ReplacementReasonCode: "TRY_FISH"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unsafeReplaceResponse.StatusCode);

        // 3. Valid replacement updates the meal atomically and creates an audit trail entry
        var validReplaceResponse = await client.PostAsJsonAsync(
            $"/api/v1/meal-plans/{plan.Id}/meals/{targetMeal.Id}/replace",
            new ReplacePlanMealByRouteRequest(
                TargetCandidateCode: null,
                ReplacementReasonCode: "WANT_VARIETY",
                ReasonNotes: "Muốn đổi món trưa khác"));
        Assert.Equal(HttpStatusCode.OK, validReplaceResponse.StatusCode);

        using var replaceDoc = JsonDocument.Parse(await validReplaceResponse.Content.ReadAsStringAsync());
        var replacedMeal = replaceDoc.RootElement.GetProperty("replacedMeal");
        var audit = replaceDoc.RootElement.GetProperty("replacementAudit");

        var newCandidateCode = replacedMeal.GetProperty("candidateCode").GetString()!;
        Assert.NotEqual(targetMeal.CandidateCode, newCandidateCode);
        Assert.Equal(targetMeal.CandidateCode, audit.GetProperty("previousCandidateCode").GetString());
        Assert.Equal(newCandidateCode, audit.GetProperty("replacementCandidateCode").GetString());
        Assert.Equal("WANT_VARIETY", audit.GetProperty("replacementReasonCode").GetString());

        // 4. Verify replacement history endpoint returns the audit record
        var historyResponse = await client.GetAsync($"/api/v1/meal-plans/{plan.Id}/replacements");
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        using var historyDoc = JsonDocument.Parse(await historyResponse.Content.ReadAsStringAsync());
        Assert.Equal(1, historyDoc.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task ReplacePlannedMeal_EnforcesOwnership_AndRejectsSupersededPlanConflict()
    {
        await _factory.ResetDatabaseAsync();
        using var ownerClient = await CreateAuthenticatedClientAsync("w2-owner@example.com");
        using var otherUserClient = await CreateAuthenticatedClientAsync("w2-intruder@example.com");

        var firstPlanResponse = await ownerClient.PostAsJsonAsync(
            "/api/v1/meal-plans/generate",
            new GenerateMealPlanRequest(
                StartDate: new DateOnly(2026, 11, 2),
                Days: 7,
                DietaryPattern: "PlantForward"));
        var firstPlan = await firstPlanResponse.Content.ReadFromJsonAsync<MealPlanResponse>();
        Assert.NotNull(firstPlan);
        var firstMealId = firstPlan.Days[0].Meals[0].Id;

        // Intruder cannot replace a meal in owner's plan -> 404 NotFound
        var intruderAttempt = await otherUserClient.PostAsJsonAsync(
            $"/api/v1/meal-plans/{firstPlan.Id}/meals/{firstMealId}/replace",
            new ReplacePlanMealByRouteRequest());
        Assert.Equal(HttpStatusCode.NotFound, intruderAttempt.StatusCode);

        // Generate a second plan for owner so firstPlan becomes Superseded
        var secondPlanResponse = await ownerClient.PostAsJsonAsync(
            "/api/v1/meal-plans/generate",
            new GenerateMealPlanRequest(
                StartDate: new DateOnly(2026, 11, 2),
                Days: 7,
                DietaryPattern: "PlantForward"));
        Assert.Equal(HttpStatusCode.Created, secondPlanResponse.StatusCode);

        // Replacing a meal in a Superseded plan returns 409 Conflict
        var stalePlanAttempt = await ownerClient.PostAsJsonAsync(
            $"/api/v1/meal-plans/{firstPlan.Id}/meals/{firstMealId}/replace",
            new ReplacePlanMealByRouteRequest());
        Assert.Equal(HttpStatusCode.Conflict, stalePlanAttempt.StatusCode);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string email)
    {
        var client = _factory.CreateClient();
        var registerResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequest(email, "Longevity#2026", "Member W2 Test"));

        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        var session = await registerResponse.Content.ReadFromJsonAsync<AuthSessionResponse>();
        Assert.NotNull(session);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return client;
    }
}

