using LongevityDiet.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LongevityDiet.Worker.Reminders;

public sealed class ReminderSchedule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public ReminderCategory Category { get; set; }
    public string TimeZone { get; set; } = "UTC";
    public TimeOnly LocalTime { get; set; }
    public TimeOnly QuietStart { get; set; }
    public TimeOnly QuietEnd { get; set; }
    public bool Enabled { get; set; }
    public int PreferenceVersion { get; set; }
    public DateTime? NextDueUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class ReminderDispatchRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public ReminderCategory Category { get; set; }
    public DateTime DueAtUtc { get; set; }
    public string DeduplicationKey { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class WorkerProcessedEvent
{
    public Guid EventId { get; set; }
    public DateTime ProcessedAtUtc { get; set; }
}

public sealed class WorkerOutboxEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public string EventType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public int PublishAttempts { get; set; }
}

public sealed class ReminderWorkerDbContext(DbContextOptions<ReminderWorkerDbContext> options) : DbContext(options)
{
    public DbSet<ReminderSchedule> Schedules => Set<ReminderSchedule>();
    public DbSet<ReminderDispatchRecord> DispatchRecords => Set<ReminderDispatchRecord>();
    public DbSet<WorkerProcessedEvent> ProcessedEvents => Set<WorkerProcessedEvent>();
    public DbSet<WorkerOutboxEvent> Outbox => Set<WorkerOutboxEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var schedules = modelBuilder.Entity<ReminderSchedule>();
        schedules.ToTable("ReminderSchedules");
        schedules.HasKey(s => s.Id);
        schedules.Property(s => s.Category).HasConversion<string>().HasMaxLength(32);
        schedules.Property(s => s.TimeZone).HasMaxLength(100);
        schedules.HasIndex(s => new { s.UserId, s.Category }).IsUnique();
        schedules.HasIndex(s => new { s.Enabled, s.NextDueUtc });

        var dispatch = modelBuilder.Entity<ReminderDispatchRecord>();
        dispatch.ToTable("ReminderDispatchRecords");
        dispatch.HasKey(d => d.Id);
        dispatch.Property(d => d.Category).HasConversion<string>().HasMaxLength(32);
        dispatch.Property(d => d.DeduplicationKey).HasMaxLength(160);
        dispatch.HasIndex(d => d.DeduplicationKey).IsUnique();

        modelBuilder.Entity<WorkerProcessedEvent>().ToTable("WorkerProcessedEvents")
            .HasKey(e => e.EventId);

        var outbox = modelBuilder.Entity<WorkerOutboxEvent>();
        outbox.ToTable("WorkerOutbox");
        outbox.HasKey(e => e.EventId);
        outbox.Property(e => e.EventType).HasMaxLength(100);
        outbox.Property(e => e.PayloadJson).HasMaxLength(4096);
        outbox.HasIndex(e => new { e.PublishedAtUtc, e.CreatedAtUtc });
    }
}
