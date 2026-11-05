using System.Globalization;
using LongevityDiet.Services.Tracking;
using StackExchange.Redis;

namespace LongevityDiet.Worker;

public sealed class RedisWeeklyReportStreamWorker(
    IServiceScopeFactory scopeFactory,
    IConnectionMultiplexer redis,
    ILogger<RedisWeeklyReportStreamWorker> logger) : BackgroundService
{
    private const string StreamName = "longevity:weekly-report-events";
    private const string GroupName = "weekly-report-worker";
    private static readonly Action<ILogger, Exception?> RedisUnavailable =
        LoggerMessage.Define(LogLevel.Warning, new EventId(1101, nameof(RedisUnavailable)),
            "Redis unavailable; stream messages remain pending/unread and will be retried.");
    private static readonly Action<ILogger, Exception?> RedisTimeout =
        LoggerMessage.Define(LogLevel.Warning, new EventId(1102, nameof(RedisTimeout)),
            "Redis timeout; worker will retry without acknowledging messages.");
    private static readonly Action<ILogger, string, Exception?> StreamEntryFailed =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(1103, nameof(StreamEntryFailed)),
            "Stream event {StreamId} failed. It is intentionally left pending for recovery.");
    private readonly string _consumerName = $"{Environment.MachineName}-{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var db = redis.GetDatabase();
        await EnsureGroupAsync(db);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var claimed = await db.StreamAutoClaimAsync(
                    StreamName,
                    GroupName,
                    _consumerName,
                    60_000,
                    "0-0",
                    20);

                foreach (var entry in claimed.ClaimedEntries)
                    await ProcessEntryAsync(db, entry, stoppingToken);

                var entries = await db.StreamReadGroupAsync(
                    StreamName,
                    GroupName,
                    _consumerName,
                    position: ">",
                    count: 20);

                if (entries.Length == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                    continue;
                }

                foreach (var entry in entries)
                    await ProcessEntryAsync(db, entry, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (RedisConnectionException ex)
            {
                RedisUnavailable(logger, ex);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (RedisTimeoutException ex)
            {
                RedisTimeout(logger, ex);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private static async Task EnsureGroupAsync(IDatabase db)
    {
        try
        {
            await db.StreamCreateConsumerGroupAsync(StreamName, GroupName, "0-0", createStream: true);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP", StringComparison.OrdinalIgnoreCase))
        {
        }
    }

    private async Task ProcessEntryAsync(IDatabase db, StreamEntry entry, CancellationToken cancellationToken)
    {
        try
        {
            var fields = entry.Values.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString(), StringComparer.OrdinalIgnoreCase);
            if (!fields.TryGetValue("eventId", out var eventIdText) || !Guid.TryParse(eventIdText, out var eventId))
                throw new InvalidOperationException("Stream event is missing a valid eventId.");
            if (!fields.TryGetValue("eventType", out var eventType))
                throw new InvalidOperationException("Stream event is missing eventType.");

            await using var scope = scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<WorkerReliabilityService>();

            if (string.Equals(eventType, "WeeklyReportRequested.v1", StringComparison.Ordinal))
            {
                if (!fields.TryGetValue("userId", out var userText) || !Guid.TryParse(userText, out var userId))
                    throw new InvalidOperationException("Weekly report event is missing userId.");
                if (!fields.TryGetValue("weekStart", out var weekText) ||
                    !DateOnly.TryParseExact(weekText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var weekStart))
                    throw new InvalidOperationException("Weekly report event is missing weekStart.");

                var scheduled = await service.ScheduleWeeklyReportAsync(userId, weekStart, cancellationToken);
                if (!scheduled.Succeeded)
                    throw new InvalidOperationException(scheduled.ErrorMessage ?? "Failed to schedule weekly report.");
            }

            await service.RecordProcessedEventAsync(eventId, eventType, cancellationToken);
            await db.StreamAcknowledgeAsync(StreamName, GroupName, entry.Id);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            StreamEntryFailed(logger, entry.Id.ToString(), ex);
        }
    }
}
