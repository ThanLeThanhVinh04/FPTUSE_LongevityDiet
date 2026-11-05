using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LongevityDiet.Domain.Entities;
using LongevityDiet.Repositories;
using LongevityDiet.Services.Tracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LongevityDiet.IntegrationTests;

public sealed class ChallengeProgressApiTests : IClassFixture<IntegrationTestWebApplicationFactory>
{
    private readonly IntegrationTestWebApplicationFactory _factory;

    public ChallengeProgressApiTests(IntegrationTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Challenge_StartDuplicateAndOwnership_AreEnforced()
    {
        await _factory.ResetDatabaseAsync();
        using var ownerClient = _factory.CreateHttpsClient();
        var owner = await RegisterAsync(ownerClient, "owner");
        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", owner.AccessToken);

        var templateId = await GetTemplateIdAsync(ownerClient);
        var create = await ownerClient.PostAsJsonAsync("/api/v1/challenges", new { TemplateId = templateId, StartDate = (DateOnly?)null });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        using var createdJson = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var challengeId = createdJson.RootElement.GetProperty("id").GetGuid();

        var duplicate = await ownerClient.PostAsJsonAsync("/api/v1/challenges", new { TemplateId = templateId, StartDate = (DateOnly?)null });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        using var otherClient = _factory.CreateHttpsClient();
        var other = await RegisterAsync(otherClient, "other");
        otherClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", other.AccessToken);

        var crossUser = await otherClient.PutAsJsonAsync(
            $"/api/v1/challenges/{challengeId}/days/1",
            new { Completed = true });

        Assert.Equal(HttpStatusCode.NotFound, crossUser.StatusCode);
    }

    [Fact]
    public async Task Challenge_AllFourteenDays_CanCompleteAndRepeatCompletionIsIdempotent()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateHttpsClient();
        var session = await RegisterAsync(client, "complete");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

        var templateId = await GetTemplateIdAsync(client);
        var create = await client.PostAsJsonAsync("/api/v1/challenges", new { TemplateId = templateId, StartDate = (DateOnly?)null });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        using var createdJson = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var challengeId = createdJson.RootElement.GetProperty("id").GetGuid();

        await MoveChallengeIntoPastAsync(challengeId);

        for (var day = 1; day <= 14; day++)
        {
            var completion = await client.PutAsJsonAsync(
                $"/api/v1/challenges/{challengeId}/days/{day}",
                new { Completed = true });
            Assert.Equal(HttpStatusCode.OK, completion.StatusCode);
        }

        var repeated = await client.PutAsJsonAsync(
            $"/api/v1/challenges/{challengeId}/days/14",
            new { Completed = true });
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);

        using var repeatedJson = JsonDocument.Parse(await repeated.Content.ReadAsStringAsync());
        Assert.Equal("Completed", repeatedJson.RootElement.GetProperty("status").GetString());
        Assert.Equal(14, repeatedJson.RootElement.GetProperty("completedDays").GetInt32());
        Assert.Equal(100, repeatedJson.RootElement.GetProperty("progressPercent").GetInt32());
        Assert.Equal(14, repeatedJson.RootElement.GetProperty("currentStreak").GetInt32());
    }

    [Fact]
    public async Task Dashboard_EmptyUserAndDateRange_ReturnPersistedZeroSeries()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateHttpsClient();
        var session = await RegisterAsync(client, "dashboard");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = today.AddDays(-2);
        var response = await client.GetAsync($"/api/v1/dashboard/summary?fromDate={from:yyyy-MM-dd}&toDate={today:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal(0, root.GetProperty("loggedDays").GetInt32());
        Assert.Equal(0m, root.GetProperty("loggingConsistencyPercent").GetDecimal());
        Assert.Equal(3, root.GetProperty("trend").GetArrayLength());
        Assert.StartsWith("unavailable:", root.GetProperty("activityAvailability").GetString());
        Assert.StartsWith("unavailable:", root.GetProperty("ldasAvailability").GetString());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LongevityDietDbContext>();
        Assert.Equal(3, await db.DailyWellnessSummaries.CountAsync(x => x.UserId == session.User.Id));
    }

    [Fact]
    public async Task ProjectionEvent_Replay_IsIdempotent()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateHttpsClient();
        var session = await RegisterAsync(client, "projection");
        var eventId = Guid.NewGuid();
        var localDate = DateOnly.FromDateTime(DateTime.UtcNow);

        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ChallengeProgressService>();

        var first = await service.ApplyProjectionEventAsync(
            new ProgressProjectionInput(eventId, "MealLogChanged.v1", session.User.Id, localDate));
        var second = await service.ApplyProjectionEventAsync(
            new ProgressProjectionInput(eventId, "MealLogChanged.v1", session.User.Id, localDate));

        Assert.True(first.Succeeded);
        Assert.True(first.Value);
        Assert.True(second.Succeeded);
        Assert.False(second.Value);

        var db = scope.ServiceProvider.GetRequiredService<LongevityDietDbContext>();
        Assert.Equal(1, await db.ProgressEventReceipts.CountAsync(x => x.EventId == eventId));
        Assert.Equal(1, await db.DailyWellnessSummaries.CountAsync(x => x.UserId == session.User.Id && x.LocalDate == localDate));
    }

    [Fact]
    public async Task Challenge_UncompleteBreaksStreakWithoutBackfill()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateHttpsClient();
        var session = await RegisterAsync(client, "streak");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var templateId = await GetTemplateIdAsync(client);

        var create = await client.PostAsJsonAsync("/api/v1/challenges", new { TemplateId = templateId, StartDate = (DateOnly?)null });
        using var createdJson = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var challengeId = createdJson.RootElement.GetProperty("id").GetGuid();
        await MoveChallengeIntoPastAsync(challengeId);

        foreach (var day in new[] { 12, 13, 14 })
        {
            var response = await client.PutAsJsonAsync($"/api/v1/challenges/{challengeId}/days/{day}", new { Completed = true });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var uncomplete = await client.PutAsJsonAsync($"/api/v1/challenges/{challengeId}/days/13", new { Completed = false });
        Assert.Equal(HttpStatusCode.OK, uncomplete.StatusCode);
        using var json = JsonDocument.Parse(await uncomplete.Content.ReadAsStringAsync());
        Assert.Equal(1, json.RootElement.GetProperty("currentStreak").GetInt32());
        Assert.Equal(2, json.RootElement.GetProperty("completedDays").GetInt32());
    }

    private async Task MoveChallengeIntoPastAsync(Guid challengeId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LongevityDietDbContext>();
        var challenge = await db.UserChallenges.Include(x => x.Days).SingleAsync(x => x.Id == challengeId);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        challenge.StartDate = today.AddDays(-13);
        challenge.EndDate = today;
        foreach (var day in challenge.Days)
            day.LocalDate = challenge.StartDate.AddDays(day.DayNumber - 1);
        await db.SaveChangesAsync();
    }

    private static async Task<Guid> GetTemplateIdAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/challenges/templates");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var template = Assert.Single(json.RootElement.EnumerateArray());
        Assert.Equal(14, template.GetProperty("durationDays").GetInt32());
        Assert.Equal(14, template.GetProperty("days").GetArrayLength());
        return template.GetProperty("id").GetGuid();
    }

    private static async Task<AuthSessionResponse> RegisterAsync(HttpClient client, string prefix)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var request = new
        {
            Email = $"{prefix}-{suffix}@example.test",
            Password = "StrongPass123!",
            DisplayName = "Challenge Tester",
        };
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<AuthSessionResponse>();
        Assert.NotNull(session);
        return session;
    }

    private sealed record AuthSessionResponse(
        string AccessToken,
        DateTimeOffset AccessTokenExpiresAt,
        SessionUserResponse User);

    private sealed record SessionUserResponse(
        Guid Id,
        string Email,
        string DisplayName,
        string Role,
        bool ProfileCompleted);
}
