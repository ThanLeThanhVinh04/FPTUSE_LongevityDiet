using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace LongevityDiet.Worker.Reminders;

/// <summary>
/// Reads Identity-published events and owns all schedule/dispatch state in WorkerDb.
/// Publishes worker results back to Redis; never touches IdentityDb.
/// </summary>
public sealed partial class ReminderWorkerHostedService(
    IServiceScopeFactory scopes, IConfiguration configuration,
    TimeProvider clock, ILogger<ReminderWorkerHostedService> logger) : BackgroundService
{


    [LoggerMessage(EventId = 4301, Level = LogLevel.Warning,
        Message = "Reminder Worker disabled: Redis connection is not configured")]
    private static partial void WarnRedisDisabled(ILogger logger);

    [LoggerMessage(EventId = 4302, Level = LogLevel.Information,
        Message = "Reminder Worker connected to Redis Streams")]
    private static partial void LogRedisConnected(ILogger logger);

    [LoggerMessage(EventId = 4303, Level = LogLevel.Warning,
        Message = "Reminder Worker disconnected; retrying after delay")]
    private static partial void WarnDisconnected(ILogger logger, Exception error);

    [LoggerMessage(EventId = 4304, Level = LogLevel.Warning,
        Message = "Invalid reminder event moved to dead-letter stream")]
    private static partial void WarnInvalidEvent(ILogger logger, Exception error);

    [LoggerMessage(EventId = 4305, Level = LogLevel.Warning,
        Message = "Worker result publish failed: {EventId}")]
    private static partial void WarnPublishFailed(ILogger logger, Guid eventId, Exception error);
    private const string PreferenceStream = "ldc:identity-events";
    private const string NotificationStream = "ldc:worker-events";
    private const string GroupName = "ldc-reminder-workers";
    private const string ConsumerName = "reminder-worker"; // stable for pending replay
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var redisConnection = configuration["Redis:ConnectionString"];
        if (string.IsNullOrWhiteSpace(redisConnection))
        {
            WarnRedisDisabled(logger);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var mux = await ConnectionMultiplexer.ConnectAsync(redisConnection);
                var redis = mux.GetDatabase();
                await EnsureGroupAsync(redis);
                LogRedisConnected(logger);
                while (!stoppingToken.IsCancellationRequested)
                {
                    await ReceiveAsync(redis, stoppingToken);
                    using (var scope = scopes.CreateScope())
                    {
                        var engine = scope.ServiceProvider.GetRequiredService<ReminderWorkerEngine>();
                        await engine.ScheduleDueAsync(stoppingToken);
                    }
                    await PublishOutboxAsync(redis, stoppingToken);
                    await Task.Delay(TimeSpan.FromSeconds(4), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception error)
            {
                WarnDisconnected(logger, error);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private static async Task EnsureGroupAsync(IDatabase redis)
    {
        try
        {
            await redis.StreamCreateConsumerGroupAsync(
                PreferenceStream, GroupName, "0-0", createStream: true);
        }
        catch (RedisServerException error) when (error.Message.Contains("BUSYGROUP", StringComparison.Ordinal))
        {
            // Consumer group already exists after restart.
        }
    }

    private async Task ReceiveAsync(IDatabase redis, CancellationToken ct)
    {
        foreach (var position in new[] { "0-0", ">" })
        {
            var entries = await redis.StreamReadGroupAsync(
                PreferenceStream, GroupName, ConsumerName, position, count: 25);
            foreach (var entry in entries)
            {
                try
                {
                    var kind = entry.Values.FirstOrDefault(p => p.Name == "eventType").Value.ToString();
                    if (!string.Equals(kind, "ReminderPreferenceUpdated.v1", StringComparison.Ordinal))
                    {
                        await redis.StreamAcknowledgeAsync(PreferenceStream, GroupName, entry.Id);
                        continue;
                    }
                    var payload = entry.Values.FirstOrDefault(p => p.Name == "payload").Value.ToString();
                    var data = JsonSerializer.Deserialize<ReminderPreferenceChangedEvent>(payload, JsonOptions)
                        ?? throw new JsonException("Reminder preference event body was missing");
                    using var scope = scopes.CreateScope();
                    var engine = scope.ServiceProvider.GetRequiredService<ReminderWorkerEngine>();
                    await engine.ApplyPreferenceAsync(data, ct);
                    await redis.StreamAcknowledgeAsync(PreferenceStream, GroupName, entry.Id);
                }
                catch (JsonException error)
                {
                    WarnInvalidEvent(logger, error);
                    await redis.StreamAddAsync("ldc:dead-letter",
                        new[] { new NameValueEntry("source", PreferenceStream),
                            new NameValueEntry("entryId", entry.Id) });
                    await redis.StreamAcknowledgeAsync(PreferenceStream, GroupName, entry.Id);
                }
            }
            // Always advance from pending replay to fresh '>' events.
            // A short/empty pending batch must not starve new messages.
        }
    }

    private async Task PublishOutboxAsync(IDatabase redis, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReminderWorkerDbContext>();
        var events = await db.Outbox
            .Where(e => e.PublishedAtUtc == null && e.PublishAttempts < 5)
            .OrderBy(e => e.CreatedAtUtc).Take(50).ToListAsync(ct);

        foreach (var item in events)
        {
            try
            {
                await redis.StreamAddAsync(NotificationStream,
                    new[]
                    {
                        new NameValueEntry("eventId", item.EventId.ToString()),
                        new NameValueEntry("eventType", item.EventType),
                        new NameValueEntry("payload", item.PayloadJson),
                    });
                item.PublishedAtUtc = clock.GetUtcNow().UtcDateTime;
            }
            catch (RedisException error)
            {
                item.PublishAttempts++;
                WarnPublishFailed(logger, item.EventId, error);
            }
            await db.SaveChangesAsync(ct);
        }
        // Retry 5 is a bounded safety stop; operator can inspect/retry the row.
        // No dead-letter auto-ack for unpublished results.
    }
}
