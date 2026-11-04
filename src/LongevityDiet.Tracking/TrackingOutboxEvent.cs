namespace LongevityDiet.Tracking;

/// <summary>
/// Transactional outbox owned by the Tracking database. No worker-side query
/// into Tracking SQL is allowed; API publishes these to Redis Streams.
/// </summary>
public sealed class TrackingOutboxEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public int PublishAttempts { get; set; }
}
