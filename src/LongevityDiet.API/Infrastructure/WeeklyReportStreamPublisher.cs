using System.Globalization;
using StackExchange.Redis;

namespace LongevityDiet.API.Infrastructure;

public sealed class WeeklyReportStreamPublisher(IConfiguration configuration)
{
    public const string StreamName = "longevity:weekly-report-events";

    public async Task<string> PublishRequestedAsync(Guid userId, DateOnly weekStart)
    {
        var connectionString = configuration["Redis:ConnectionString"]
            ?? throw new InvalidOperationException("Redis:ConnectionString is required to queue weekly report jobs.");

        await using var connection = await ConnectionMultiplexer.ConnectAsync(connectionString);
        var db = connection.GetDatabase();
        var eventId = Guid.NewGuid();
        var id = await db.StreamAddAsync(StreamName,
        [
            new NameValueEntry("eventId", eventId.ToString("D")),
            new NameValueEntry("eventType", "WeeklyReportRequested.v1"),
            new NameValueEntry("userId", userId.ToString("D")),
            new NameValueEntry("weekStart", weekStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            new NameValueEntry("occurredAt", DateTimeOffset.UtcNow.ToString("O")),
        ]);
        return id.ToString();
    }
}
