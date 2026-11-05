using System.Net;
using System.Net.Http.Json;
using LongevityDiet.Domain.Entities;
using LongevityDiet.Repositories;
using LongevityDiet.Services.Tracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LongevityDiet.IntegrationTests;

public sealed class WeeklyReportWorkerTests : IClassFixture<IntegrationTestWebApplicationFactory>
{
    private readonly IntegrationTestWebApplicationFactory _factory;

    public WeeklyReportWorkerTests(IntegrationTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task WeeklyReport_Generation_IsIdempotentAndHistoricalResultIsNotRewritten()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateHttpsClient();
        var userId = await RegisterAsync(client, "weekly");

        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<WeeklyReportService>();
        var week = WeeklyReportService.StartOfWeek(DateOnly.FromDateTime(DateTime.UtcNow));

        var first = await service.GenerateAsync(userId, week);
        var second = await service.GenerateAsync(userId, week);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(first.Value!.Id, second.Value!.Id);

        var db = scope.ServiceProvider.GetRequiredService<LongevityDietDbContext>();
        Assert.Equal(1, await db.WeeklyReports.CountAsync(x => x.UserId == userId && x.WeekStart == week));
        Assert.Equal(8, await db.WeeklyReportMetrics.CountAsync(x => x.WeeklyReportId == first.Value.Id));
    }

    [Fact]
    public async Task DuplicateWeeklyJob_UsesSameIdempotencyKey()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateHttpsClient();
        var userId = await RegisterAsync(client, "job");

        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<WorkerReliabilityService>();
        var week = WeeklyReportService.StartOfWeek(DateOnly.FromDateTime(DateTime.UtcNow));

        var first = await service.ScheduleWeeklyReportAsync(userId, week);
        var second = await service.ScheduleWeeklyReportAsync(userId, week);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(first.Value!.Id, second.Value!.Id);

        var db = scope.ServiceProvider.GetRequiredService<LongevityDietDbContext>();
        Assert.Equal(1, await db.WorkerJobs.CountAsync(x => x.IdempotencyKey == first.Value.IdempotencyKey));
    }

    [Fact]
    public async Task ProcessedEvent_DuplicateDelivery_IsIgnored()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<WorkerReliabilityService>();
        var eventId = Guid.NewGuid();

        var first = await service.RecordProcessedEventAsync(eventId, "WeeklyReportGenerated.v1");
        var second = await service.RecordProcessedEventAsync(eventId, "WeeklyReportGenerated.v1");

        Assert.True(first.Value);
        Assert.False(second.Value);

        var db = scope.ServiceProvider.GetRequiredService<LongevityDietDbContext>();
        Assert.Equal(1, await db.WorkerProcessedEvents.CountAsync(x => x.EventId == eventId));
    }

    [Fact]
    public async Task PoisonJob_DeadLettersAndCanBeRecovered()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LongevityDietDbContext>();
        var now = DateTimeOffset.UtcNow;
        var job = new WorkerJob
        {
            Id = Guid.NewGuid(),
            JobType = "UnsupportedJob",
            IdempotencyKey = "poison:" + Guid.NewGuid().ToString("N"),
            MaxAttempts = 1,
            Status = WorkerJobStatus.Pending,
            NextAttemptAt = now.AddMinutes(-1),
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.WorkerJobs.Add(job);
        await db.SaveChangesAsync();

        var service = scope.ServiceProvider.GetRequiredService<WorkerReliabilityService>();
        var processed = await service.ProcessRunnableJobsAsync("test-instance");

        Assert.Equal(0, processed);
        await db.Entry(job).ReloadAsync();
        Assert.Equal(WorkerJobStatus.DeadLetter, job.Status);

        var dead = await db.WorkerDeadLetters.SingleAsync(x => x.WorkerJobId == job.Id);
        var recovery = await service.RecoverDeadLetterAsync(dead.Id);

        Assert.True(recovery.Succeeded);
        await db.Entry(job).ReloadAsync();
        Assert.Equal(WorkerJobStatus.Pending, job.Status);
        Assert.Equal(0, job.AttemptCount);
    }

    [Fact]
    public async Task StaleProcessingJob_IsReturnedToPendingForRecovery()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LongevityDietDbContext>();
        var now = DateTimeOffset.UtcNow;
        var job = new WorkerJob
        {
            Id = Guid.NewGuid(),
            JobType = "WeeklyReport",
            IdempotencyKey = "stale:" + Guid.NewGuid().ToString("N"),
            Status = WorkerJobStatus.Processing,
            NextAttemptAt = now,
            CreatedAt = now.AddMinutes(-10),
            UpdatedAt = now.AddMinutes(-10),
            LockedAt = now.AddMinutes(-10),
            LockedBy = "crashed-instance",
        };
        db.WorkerJobs.Add(job);
        await db.SaveChangesAsync();

        var service = scope.ServiceProvider.GetRequiredService<WorkerReliabilityService>();
        await service.RecoverStaleProcessingJobsAsync(TimeSpan.FromMinutes(2));

        await db.Entry(job).ReloadAsync();
        Assert.Equal(WorkerJobStatus.Pending, job.Status);
        Assert.Null(job.LockedBy);
        Assert.Null(job.LockedAt);
    }

    private static async Task<Guid> RegisterAsync(HttpClient client, string prefix)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email = $"{prefix}-{Guid.NewGuid():N}@example.test",
            Password = "StrongPass123!",
            DisplayName = "Weekly Tester",
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<AuthSession>();
        Assert.NotNull(payload);
        return payload.User.Id;
    }

    private sealed record AuthSession(string AccessToken, DateTimeOffset AccessTokenExpiresAt, SessionUser User);
    private sealed record SessionUser(Guid Id, string Email, string DisplayName, string Role, bool ProfileCompleted);
}
