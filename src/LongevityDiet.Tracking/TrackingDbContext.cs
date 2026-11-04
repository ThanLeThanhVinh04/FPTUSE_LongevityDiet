using Microsoft.EntityFrameworkCore;

namespace LongevityDiet.Tracking;

// Target ownership: LongevityTrackingDb; never holds identity/catalog entities.
public sealed class TrackingDbContext(DbContextOptions<TrackingDbContext> options) : DbContext(options)
{
    public DbSet<ActivityLog> Activities => Set<ActivityLog>();
    public DbSet<ActivityGoal> Goals => Set<ActivityGoal>();
    public DbSet<ScoreConfigurationVersion> ScoreConfigurations => Set<ScoreConfigurationVersion>();
    public DbSet<AdherenceScore> Scores => Set<AdherenceScore>();
    public DbSet<ScoreDimension> ScoreDimensions => Set<ScoreDimension>();
    public DbSet<TrackingScoreSignal> ScoreSignals => Set<TrackingScoreSignal>();
    public DbSet<TrackingOutboxEvent> Outbox => Set<TrackingOutboxEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var outbox = modelBuilder.Entity<TrackingOutboxEvent>();
        outbox.ToTable("TrackingOutbox");
        outbox.HasKey(x => x.EventId);
        outbox.Property(x => x.EventType).HasMaxLength(100);
        outbox.Property(x => x.PayloadJson).HasMaxLength(4096);
        outbox.HasIndex(x => new { x.PublishedAtUtc, x.CreatedAtUtc });

        var activities = modelBuilder.Entity<ActivityLog>();
        activities.ToTable("ActivityLogs");
        activities.HasKey(a => a.Id);
        activities.Property(a => a.Kind).HasConversion<string>().HasMaxLength(24);
        activities.Property(a => a.Note).HasMaxLength(800);
        activities.HasIndex(a => new { a.UserId, a.StartedAtUtc });
        activities.HasIndex(a => new { a.UserId, a.IsDeleted, a.StartedAtUtc });

        var goals = modelBuilder.Entity<ActivityGoal>();
        goals.ToTable("ActivityGoals");
        goals.HasKey(g => g.UserId);

        var versions = modelBuilder.Entity<ScoreConfigurationVersion>();
        versions.ToTable("ScoreConfigurationVersions");
        versions.HasKey(v => v.Version);
        versions.Property(v => v.Version).HasMaxLength(40);
        versions.Property(v => v.SourceReference).HasMaxLength(512);
        versions.Property(v => v.ActivityWeight).HasPrecision(5, 4);
        versions.Property(v => v.MealWeight).HasPrecision(5, 4);
        versions.Property(v => v.EatingWindowWeight).HasPrecision(5, 4);
        versions.HasData(new ScoreConfigurationVersion
        {
            Version = "ldas-v1",
            Published = true,
            PublishedAtUtc = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero),
            ActivityWeight = 0.35m,
            MealWeight = 0.45m,
            EatingWindowWeight = 0.20m,
            SourceReference = "project-scoring-policy-v1; educational, not clinically validated",
        });

        var scores = modelBuilder.Entity<AdherenceScore>();
        scores.ToTable("AdherenceScores");
        scores.HasKey(s => s.Id);
        scores.Property(s => s.Version).HasMaxLength(40);
        scores.Property(s => s.Score).HasPrecision(5, 2);
        scores.Property(s => s.CoveragePercent).HasPrecision(5, 2);
        scores.Property(s => s.MissingDataJson).HasMaxLength(512);
        scores.HasIndex(s => new { s.UserId, s.LocalDate, s.Version }).IsUnique();
        scores.HasMany(s => s.Dimensions).WithOne(d => d.AdherenceScore)
            .HasForeignKey(d => d.AdherenceScoreId).OnDelete(DeleteBehavior.Cascade);

        var dimensions = modelBuilder.Entity<ScoreDimension>();
        dimensions.ToTable("ScoreDimensions");
        dimensions.HasKey(d => d.Id);
        dimensions.Property(d => d.Code).HasMaxLength(32);
        dimensions.Property(d => d.ReasonCode).HasMaxLength(128);
        dimensions.Property(d => d.Weight).HasPrecision(5, 4);
        dimensions.Property(d => d.RawScore).HasPrecision(5, 2);
        dimensions.Property(d => d.WeightedScore).HasPrecision(5, 2);
        dimensions.HasIndex(d => new { d.AdherenceScoreId, d.Code }).IsUnique();

        var signals = modelBuilder.Entity<TrackingScoreSignal>();
        signals.ToTable("TrackingScoreSignals");
        signals.HasKey(s => s.Id);
        signals.Property(s => s.Percent).HasPrecision(5, 2);
        signals.Property(s => s.DimensionCode).HasMaxLength(32);
        signals.Property(s => s.SourceVersion).HasMaxLength(64);
        signals.HasIndex(s => new { s.UserId, s.LocalDate, s.DimensionCode }).IsUnique();
        signals.HasIndex(s => s.SourceEventId).IsUnique();
    }
}
