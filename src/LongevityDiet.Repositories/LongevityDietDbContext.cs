using LongevityDiet.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LongevityDiet.Repositories;

public sealed class LongevityDietDbContext(DbContextOptions<LongevityDietDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<ReminderPreference> ReminderPreferences => Set<ReminderPreference>();
    public DbSet<UserNotification> Notifications => Set<UserNotification>();
    public DbSet<IdentityOutboxEvent> IdentityOutbox => Set<IdentityOutboxEvent>();
    public DbSet<IdentityInboxEvent> IdentityInbox => Set<IdentityInboxEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var user = modelBuilder.Entity<User>();
        user.ToTable("Users");
        user.HasKey(x => x.Id);
        user.Property(x => x.Email).HasMaxLength(320).IsRequired();
        user.Property(x => x.NormalizedEmail).HasMaxLength(320).IsRequired();
        user.HasIndex(x => x.NormalizedEmail).IsUnique();
        user.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
        user.Property(x => x.DisplayName).HasMaxLength(120).IsRequired();
        user.Property(x => x.Role).HasConversion<string>().HasMaxLength(24);
        user.Property(x => x.Status).HasConversion<string>().HasMaxLength(24);
        user.HasOne(x => x.Profile)
            .WithOne(x => x.User)
            .HasForeignKey<UserProfile>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        var profile = modelBuilder.Entity<UserProfile>();
        profile.ToTable("UserProfiles");
        profile.HasKey(x => x.UserId);
        profile.Property(x => x.TimeZone).HasMaxLength(100).IsRequired();
        profile.Property(x => x.FoodPreference).HasMaxLength(500);

        var refreshToken = modelBuilder.Entity<RefreshToken>();
        refreshToken.ToTable("RefreshTokens");
        refreshToken.HasKey(x => x.Id);
        refreshToken.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        refreshToken.HasIndex(x => x.TokenHash).IsUnique();
        refreshToken.HasIndex(x => new { x.UserId, x.ExpiresAt });
        refreshToken.HasOne(x => x.User)
            .WithMany(x => x.RefreshTokens)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        var preferences = modelBuilder.Entity<ReminderPreference>();
        preferences.ToTable("ReminderPreferences");
        preferences.HasKey(p => p.UserId);
        preferences.HasOne<User>().WithOne()
            .HasForeignKey<ReminderPreference>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        var notifications = modelBuilder.Entity<UserNotification>();
        notifications.ToTable("UserNotifications");
        notifications.HasKey(n => n.Id);
        notifications.Property(n => n.Category).HasConversion<string>().HasMaxLength(32);
        notifications.Property(n => n.Title).HasMaxLength(120);
        notifications.Property(n => n.Body).HasMaxLength(500);
        notifications.Property(n => n.DeepLink).HasMaxLength(120);
        notifications.Property(n => n.DeduplicationKey).HasMaxLength(160);
        notifications.HasIndex(n => new { n.UserId, n.CreatedAtUtc });
        notifications.HasIndex(n => new { n.UserId, n.DeduplicationKey }).IsUnique();
        notifications.HasOne<User>().WithMany()
            .HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);

        var outbox = modelBuilder.Entity<IdentityOutboxEvent>();
        outbox.ToTable("IdentityOutbox");
        outbox.HasKey(o => o.Id);
        outbox.Property(o => o.EventType).HasMaxLength(96);
        outbox.Property(o => o.PayloadJson).HasMaxLength(4096);
        outbox.HasIndex(o => new { o.PublishedAtUtc, o.CreatedAtUtc });

        var inbox = modelBuilder.Entity<IdentityInboxEvent>();
        inbox.ToTable("IdentityInbox");
        inbox.HasKey(i => i.EventId);
    }
}
