using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LongevityDiet.Tracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LongevityDiet.IntegrationTests;

public sealed class ActivityLdasApiTests : IClassFixture<IntegrationTestWebApplicationFactory>
{
    private readonly IntegrationTestWebApplicationFactory _factory;

    public ActivityLdasApiTests(IntegrationTestWebApplicationFactory factory) =>
        _factory = factory;

    [Fact]
    public async Task Activities_RequireAccessTokenAndDoNotAcceptArbitraryUserId()
    {
        await _factory.ResetDatabaseAsync();
        using var anonymous = _factory.CreateHttpsClient();
        var response = await anonymous.GetAsync("/api/v1/activities");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var create = await anonymous.PostAsJsonAsync("/api/v1/activities",
            new { userId = Guid.NewGuid(), kind = 1, durationMinutes = 20, startedAt = DateTimeOffset.UtcNow });
        Assert.Equal(HttpStatusCode.Unauthorized, create.StatusCode);
    }

    [Fact]
    public async Task ActivityCrud_IsUserOwned_AndSoftDeleteRecalculates()
    {
        await _factory.ResetDatabaseAsync();
        using var alice = await AuthorizedClientAsync();
        using var bob = await AuthorizedClientAsync();
        var today = DateTimeOffset.UtcNow.AddHours(-2);
        var create = await alice.PostAsJsonAsync("/api/v1/activities", new
        {
            kind = 1, durationMinutes = 30, startedAt = today, note = "Walk",
            userId = Guid.NewGuid(), // over-posting must be ignored
        });
        Assert.True(create.StatusCode == HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        var created = await create.Content.ReadFromJsonAsync<ActivityView>();
        Assert.NotNull(created);
        Assert.Equal(30, created.DurationMinutes);

        var aliceList = await alice.GetFromJsonAsync<PageView<ActivityView>>(
            "/api/v1/activities?page=1&pageSize=10");
        Assert.Single(aliceList!.Items);
        var bobList = await bob.GetFromJsonAsync<PageView<ActivityView>>(
            "/api/v1/activities?page=1&pageSize=10");
        Assert.Empty(bobList!.Items);

        var attempt = await bob.DeleteAsync($"/api/v1/activities/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, attempt.StatusCode);

        var update = await alice.PutAsJsonAsync($"/api/v1/activities/{created.Id}",
            new { kind = 4, durationMinutes = 25, startedAt = today, note = "Strength" });
        Assert.True(update.StatusCode == HttpStatusCode.OK, await update.Content.ReadAsStringAsync());

        var deleted = await alice.DeleteAsync($"/api/v1/activities/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        var empty = await alice.GetFromJsonAsync<PageView<ActivityView>>("/api/v1/activities");
        Assert.Empty(empty!.Items);
    }

    [Fact]
    public async Task ValidationAndPagination_AreBounded()
    {
        await _factory.ResetDatabaseAsync();
        using var client = await AuthorizedClientAsync();
        foreach (var minutes in new[] { -1, 0, 721 })
        {
            var result = await client.PostAsJsonAsync("/api/v1/activities",
                new { kind = 1, durationMinutes = minutes, startedAt = DateTimeOffset.UtcNow });
            Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
            using var problem = JsonDocument.Parse(await result.Content.ReadAsStringAsync());
            Assert.Equal("invalid_duration", problem.RootElement.GetProperty("title").GetString());
        }

        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.GetAsync("/api/v1/activities?page=1&pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PutAsJsonAsync("/api/v1/activities/goal",
                new { weeklyMinutesTarget = -1, weeklyStrengthSessionsTarget = 9 })).StatusCode);
    }

    [Fact]
    public async Task Ldas_UsesRealActivityOnlyAndDoesNotInventMealOrWindowObservations()
    {
        await _factory.ResetDatabaseAsync();
        using var client = await AuthorizedClientAsync();
        var utc = DateTimeOffset.UtcNow.AddHours(-3);
        var create = await client.PostAsJsonAsync("/api/v1/activities",
            new { kind = 2, durationMinutes = 40, startedAt = utc, note = "Jog" });
        Assert.True(create.StatusCode == HttpStatusCode.Created, await create.Content.ReadAsStringAsync());

        var date = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(utc, TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")).DateTime);
        var score = await client.GetFromJsonAsync<ScoreView>($"/api/v1/ldas/{date:yyyy-MM-dd}");
        Assert.NotNull(score);
        Assert.True(score.IsPartial);
        Assert.Null(score.Score); // do not invent total LDAS with missing upstream data
        Assert.Equal(35m, score.CoveragePercent);
        Assert.False(score.IsClinical);
        Assert.Contains("meals", score.MissingData);
        Assert.Contains("eating_window", score.MissingData);
        Assert.Equal("ldas-v1", score.Version);
        Assert.Equal(3, score.Breakdown.Count);
    }

    [Fact]
    public async Task DailySummaryAndLatestScore_AreComputedFromStoredMemberEntries()
    {
        await _factory.ResetDatabaseAsync();
        using var client = await AuthorizedClientAsync();
        var now = DateTimeOffset.UtcNow;
        var startedAt = now.AddMinutes(-5);
        using var create = await client.PostAsJsonAsync("/api/v1/activities", new
        {
            kind = 1, durationMinutes = 25, startedAt, note = "Morning walk",
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var localDate = TrackingService.LocalDate(startedAt,
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"));

        var summary = await client.GetFromJsonAsync<DailyActivitySummary>(
            $"/api/v1/activities/daily?date={localDate:yyyy-MM-dd}");
        Assert.NotNull(summary);
        Assert.Equal(localDate, summary.LocalDate);
        Assert.Equal(1, summary.ActivityCount);
        Assert.Equal(25, summary.EquivalentMinutes);
        Assert.Equal("Asia/Ho_Chi_Minh", summary.TimeZone);

        var latest = await client.GetFromJsonAsync<ScoreView>("/api/v1/ldas/latest");
        Assert.NotNull(latest);
        Assert.True(latest.IsPartial);
        Assert.Null(latest.Score);
        Assert.Equal(35m, latest.CoveragePercent);
        Assert.Equal(1, latest.Breakdown.Count(d => d.Code == "activity" && d.RawScore.HasValue));
    }

    [Fact]
    public async Task WeeklySummaryAndGoal_UsePersistedActivity()
    {
        await _factory.ResetDatabaseAsync();
        using var client = await AuthorizedClientAsync();
        var goal = await client.PutAsJsonAsync("/api/v1/activities/goal",
            new { weeklyMinutesTarget = 210, weeklyStrengthSessionsTarget = 2 });
        Assert.Equal(HttpStatusCode.OK, goal.StatusCode);
        var fetchedGoal = await client.GetFromJsonAsync<GoalView>("/api/v1/activities/goal");
        Assert.Equal(210, fetchedGoal!.WeeklyMinutesTarget);

        var now = DateTimeOffset.UtcNow;
        var create = await client.PostAsJsonAsync("/api/v1/activities",
            new { kind = 3, durationMinutes = 20, startedAt = now.AddHours(-1) });
        Assert.True(create.StatusCode == HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        var summary = await client.GetFromJsonAsync<ActivitySummary>("/api/v1/activities/summary");
        Assert.NotNull(summary);
        Assert.True(summary.EquivalentMinutes >= 40);
        Assert.Equal(210, summary.WeeklyMinutesTarget);
    }

    [Fact]
    public async Task Score_RejectsFutureDates_AndConfigurationRemainsPublished()
    {
        await _factory.ResetDatabaseAsync();
        using var client = await AuthorizedClientAsync();
        var future = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(8));
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.GetAsync($"/api/v1/ldas/{future:yyyy-MM-dd}")).StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();
        var config = await db.ScoreConfigurations.AsNoTracking().SingleAsync();
        Assert.Equal("ldas-v1", config.Version);
        Assert.True(config.Published);
    }

    private async Task<HttpClient> AuthorizedClientAsync()
    {
        var client = _factory.CreateHttpsClient();
        var registration = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = $"tracking-{Guid.NewGuid():N}@example.test",
            password = "StrongPass123!",
            displayName = "Tracking Member",
        });
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        using var document = JsonDocument.Parse(await registration.Content.ReadAsStringAsync());
        var token = document.RootElement.GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
