using System.Net;
using System.Net.Http.Json;
using LongevityDiet.Tracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LongevityDiet.IntegrationTests;

public sealed class Member1SecurityApiTests
{
    [Fact]
    public async Task LivenessAndReadiness_IndependentlyReportDependencyStatus()
    {
        using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateHttpsClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/ready")).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var tracking = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();
            await tracking.Database.EnsureDeletedAsync();
        }
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/ready")).StatusCode);
    }

    [Fact]
    public async Task CorrelationId_PreservesSafeValueAndRejectsHeaderInjection()
    {
        using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateHttpsClient();
        using var first = new HttpRequestMessage(HttpMethod.Get, "/health");
        first.Headers.TryAddWithoutValidation("X-Correlation-ID", "task-member1_2026");
        using var firstReply = await client.SendAsync(first);
        Assert.Equal("task-member1_2026", firstReply.Headers.GetValues("X-Correlation-ID").Single());

        using var second = new HttpRequestMessage(HttpMethod.Get, "/health");
        second.Headers.TryAddWithoutValidation("X-Correlation-ID", new string('!', 70));
        using var secondReply = await client.SendAsync(second);
        var generated = secondReply.Headers.GetValues("X-Correlation-ID").Single();
        Assert.Equal(32, generated.Length);
        Assert.NotEqual(new string('!', 70), generated);
    }

    [Fact]
    public async Task AuthRateLimit_Returns429WithProblemDetailsAndRetryAfter()
    {
        using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateHttpsClient();
        var blocked = false;
        for (var attempt = 0; attempt < 44; attempt++)
        {
            using var response = await client.PostAsync("/api/v1/auth/refresh", null);
            if (response.StatusCode != HttpStatusCode.TooManyRequests)
                continue;
            blocked = true;
            Assert.Equal(60, response.Headers.RetryAfter?.Delta?.TotalSeconds ?? 0);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            break;
        }
        Assert.True(blocked, "Auth requests must be throttled after configured permit limit.");
    }
}
