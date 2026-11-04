using LongevityDiet.Domain.Entities;
using LongevityDiet.Worker.Reminders;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LongevityDiet.IntegrationTests;

public sealed class ReminderWorkerEngineTests
{
    private static readonly Guid UserId = Guid.Parse("123e4567-e89b-42d3-a456-426614174000");

    [Fact]
    public async Task PreferenceEventCreatesOwnedSchedules_AndDuplicateIsIgnored()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateDb(connection);
        await context.Database.EnsureCreatedAsync();

        var engine = new ReminderWorkerEngine(context, TimeProvider.System);
        var evt = MakeEvent(1);
        Assert.True(await engine.ApplyPreferenceAsync(evt, default));
        Assert.Equal(4, await context.Schedules.CountAsync());
        Assert.Equal(2, await context.Schedules.CountAsync(x => x.Enabled));
        Assert.False(await engine.ApplyPreferenceAsync(evt, default));
        Assert.Equal(1, await context.ProcessedEvents.CountAsync());
    }

    [Fact]
    public async Task OutOfOrderPreferenceVersionDoesNotReplaceNewState()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateDb(connection);
        await context.Database.EnsureCreatedAsync();

        var engine = new ReminderWorkerEngine(context, TimeProvider.System);
        Assert.True(await engine.ApplyPreferenceAsync(MakeEvent(3), default));
        var older = MakeEvent(2) with { EventId = Guid.NewGuid(), Enabled = false };
        Assert.False(await engine.ApplyPreferenceAsync(older, default));
        var rows = await context.Schedules.ToListAsync();
        Assert.All(rows, r => Assert.Equal(3, r.PreferenceVersion));
        Assert.Equal(2, rows.Count(r => r.Enabled));
    }

    [Fact]
    public async Task DueReminder_CreatesSingleDispatchAndDurableWorkerResult()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateDb(connection);
        await context.Database.EnsureCreatedAsync();

        var engine = new ReminderWorkerEngine(context, TimeProvider.System);
        await engine.ApplyPreferenceAsync(MakeEvent(1), default);
        var row = await context.Schedules.SingleAsync(r => r.Category == ReminderCategory.Activity);
        row.NextDueUtc = DateTime.UtcNow.AddSeconds(-30);
        await context.SaveChangesAsync();

        Assert.Equal(1, await engine.ScheduleDueAsync(default));
        Assert.Equal(0, await engine.ScheduleDueAsync(default));
        Assert.Equal(1, await context.DispatchRecords.CountAsync());
        var outgoing = await context.Outbox.SingleAsync();
        Assert.Equal("NotificationReady.v1", outgoing.EventType);
        Assert.Contains("deduplicationKey", outgoing.PayloadJson);
    }

    [Fact]
    public async Task OptedOutReminder_NeverDispatches()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateDb(connection);
        await context.Database.EnsureCreatedAsync();
        var engine = new ReminderWorkerEngine(context, TimeProvider.System);
        await engine.ApplyPreferenceAsync(MakeEvent(1) with { Enabled = false }, default);
        Assert.Equal(0, await engine.ScheduleDueAsync(default));
        Assert.Empty(await context.Outbox.ToListAsync());
    }

    private static ReminderWorkerDbContext CreateDb(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<ReminderWorkerDbContext>().UseSqlite(connection).Options);

    private static ReminderPreferenceChangedEvent MakeEvent(int version) => new(
        Guid.NewGuid(),
        UserId,
        "Asia/Ho_Chi_Minh",
        true, true,
        true, true, false, false,
        new TimeOnly(20, 0),
        new TimeOnly(18, 30),
        new TimeOnly(21, 0),
        new TimeOnly(9, 0),
        new TimeOnly(22, 0),
        new TimeOnly(7, 0),
        version);
}
