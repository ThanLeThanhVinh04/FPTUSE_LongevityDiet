using System.Text.Json;
using LongevityDiet.Repositories;
using LongevityDiet.Tracking;
using LongevityDiet.Services.Reminders;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace LongevityDiet.API.Reminders;

/// <summary>
/// Transitional Identity-owned outbox publisher and results consumer.
/// All Identity DB reads/writes occur ONLY inside Identity API scopes.
/// </summary>
public sealed partial class IdentityRedisBridge(
    IServiceScopeFactory scopes,
    IConfiguration configuration,
    ILogger<IdentityRedisBridge> logger,
    TimeProvider clock) : BackgroundService
{

    [LoggerMessage(EventId = 4401, Level = LogLevel.Warning,
        Message = "Identity event bridge disabled because Redis is not configured")]
    private static partial void WarnRedisDisabled(ILogger logger);

    [LoggerMessage(EventId = 4402, Level = LogLevel.Warning,
        Message = "Identity event bridge disconnected; retrying")]
    private static partial void WarnDisconnected(ILogger logger, Exception error);

    [LoggerMessage(EventId = 4403, Level = LogLevel.Warning,
        Message = "Identity outbox publish failed for {EventId}")]
    private static partial void WarnIdentityPublish(ILogger logger, Guid eventId, Exception error);

    [LoggerMessage(EventId = 4404, Level = LogLevel.Warning,
        Message = "Tracking outbox publish failed for {EventId}")]
    private static partial void WarnTrackingPublish(ILogger logger, Guid eventId, Exception error);

    [LoggerMessage(EventId = 4405, Level = LogLevel.Warning,
        Message = "Invalid notification event moved to dead-letter stream")]
    private static partial void WarnInvalidEvent(ILogger logger, Exception error);

    private const string IdentityStream = "ldc:identity-events";
    private const string WorkerStream = "ldc:worker-events";
    private const string Group = "ldc-identity-notifications";
    private const string Consumer = "identity-api";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connectionString = configuration["Redis:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            WarnRedisDisabled(logger);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var redis = await ConnectionMultiplexer.ConnectAsync(connectionString);
                var db = redis.GetDatabase();
                try
                {
                    await db.StreamCreateConsumerGroupAsync(WorkerStream, Group, "0-0", createStream: true);
                }
                catch (RedisServerException error) when (error.Message.Contains("BUSYGROUP", StringComparison.Ordinal))
                {
                    // Existing group reused after restart.
                }
                while (!stoppingToken.IsCancellationRequested)
                {
                    await PublishAsync(db, stoppingToken);
                    await PublishTrackingAsync(db, stoppingToken);
                    await ConsumeAsync(db, stoppingToken);
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
                try { await Task.Delay(RetryDelay, stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            }
        }
    }

    private async Task PublishAsync(IDatabase redis, CancellationToken stoppingToken)
    {
        using var scope = scopes.CreateScope();
        var owned = scope.ServiceProvider.GetRequiredService<LongevityDietDbContext>();
        var messages = await owned.IdentityOutbox
            .Where(e => e.PublishedAtUtc == null && e.PublishAttempts < 5)
            .OrderBy(e => e.CreatedAtUtc).Take(30).ToArrayAsync(stoppingToken);
        foreach (var message in messages)
        {
            try
            {
                await redis.StreamAddAsync(IdentityStream,
                    new[]
                    {
                        new NameValueEntry("eventId", message.Id.ToString("D")),
                        new NameValueEntry("eventType", message.EventType),
                        new NameValueEntry("payload", message.PayloadJson),
                    });
                message.PublishedAtUtc = clock.GetUtcNow().UtcDateTime;
            }
            catch (RedisException error)
            {
                message.PublishAttempts++;
                WarnIdentityPublish(logger, message.Id, error);
            }
            await owned.SaveChangesAsync(stoppingToken);
        }
    }

    private async Task PublishTrackingAsync(IDatabase redis, CancellationToken stoppingToken)
    {
        using var scope = scopes.CreateScope();
        var tracking = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();
        var events = await tracking.Outbox
            .Where(x => x.PublishedAtUtc == null && x.PublishAttempts < 5)
            .OrderBy(x => x.CreatedAtUtc).Take(30).ToArrayAsync(stoppingToken);
        foreach (var message in events)
        {
            try
            {
                await redis.StreamAddAsync("ldc:tracking-events", new[]
                {
                    new NameValueEntry("eventId", message.EventId.ToString("D")),
                    new NameValueEntry("eventType", message.EventType),
                    new NameValueEntry("payload", message.PayloadJson),
                });
                message.PublishedAtUtc = clock.GetUtcNow().UtcDateTime;
            }
            catch (RedisException error)
            {
                message.PublishAttempts++;
                WarnTrackingPublish(logger, message.EventId, error);
            }
            await tracking.SaveChangesAsync(stoppingToken);
        }
    }

    private async Task ConsumeAsync(IDatabase redis, CancellationToken stoppingToken)
    {
        foreach (var position in new[] { "0-0", ">" })
        {
            var messages = await redis.StreamReadGroupAsync(
                WorkerStream, Group, Consumer, position, count: 20);
            foreach (var message in messages)
            {
                try
                {
                    var kind = message.Values.FirstOrDefault(x => x.Name == "eventType").Value.ToString();
                    if (kind != "NotificationReady.v1")
                    {
                        await redis.StreamAcknowledgeAsync(WorkerStream, Group, message.Id);
                        continue;
                    }
                    var payload = message.Values.FirstOrDefault(x => x.Name == "payload").Value.ToString();
                    var result = JsonSerializer.Deserialize<NotificationReadyEvent>(payload, JsonOptions)
                        ?? throw new JsonException("Empty notification event");
                    using var scope = scopes.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<ReminderService>();
                    await service.PersistNotificationResultAsync(result, stoppingToken);
                    await redis.StreamAcknowledgeAsync(WorkerStream, Group, message.Id);
                }
                catch (JsonException error)
                {
                    WarnInvalidEvent(logger, error);
                    await redis.StreamAddAsync("ldc:dead-letter", new[]
                    {
                        new NameValueEntry("source", WorkerStream),
                        new NameValueEntry("entryId", message.Id),
                    });
                    await redis.StreamAcknowledgeAsync(WorkerStream, Group, message.Id);
                }
            }
            // Always check the next position. Empty pending history must not
            // prevent reading fresh '>' messages from Redis Streams.
        }
    }
}
