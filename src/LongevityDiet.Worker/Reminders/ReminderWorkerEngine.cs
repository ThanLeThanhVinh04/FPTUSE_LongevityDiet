using System.Text.Json;
using LongevityDiet.Domain.Entities;
using LongevityDiet.Services.Reminders;
using Microsoft.EntityFrameworkCore;

namespace LongevityDiet.Worker.Reminders;

public sealed record ReminderPreferenceChangedEvent(
    Guid EventId, Guid UserId, string TimeZone, bool Enabled, bool ActiveAccount,
    bool MealLoggingEnabled, bool ActivityEnabled, bool ChallengeEnabled,
    bool WeeklySummaryEnabled, TimeOnly MealTime, TimeOnly ActivityTime,
    TimeOnly ChallengeTime, TimeOnly WeeklySummaryTime,
    TimeOnly QuietStart, TimeOnly QuietEnd, int Version);

/// <summary>
/// Worker-only persistence; Identity DbContext is deliberately unavailable here.
/// All Identity changes arrive as versioned events and results return via outbox.
/// </summary>
public sealed class ReminderWorkerEngine(
    ReminderWorkerDbContext db, TimeProvider timeProvider)
{
    public async Task<bool> ApplyPreferenceAsync(
        ReminderPreferenceChangedEvent incoming, CancellationToken cancellationToken)
    {
        if (incoming.UserId == Guid.Empty || incoming.EventId == Guid.Empty ||
            incoming.Version < 1 || incoming.TimeZone.Length > 100)
            throw new InvalidOperationException("Invalid preference event.");
        if (await db.ProcessedEvents.AnyAsync(p => p.EventId == incoming.EventId, cancellationToken))
            return false;

        var zone = TimeZoneInfo.FindSystemTimeZoneById(incoming.TimeZone);
        var pref = new ReminderPreference
        {
            UserId = incoming.UserId,
            OptedIn = incoming.Enabled && incoming.ActiveAccount,
            MealLoggingEnabled = incoming.MealLoggingEnabled,
            ActivityEnabled = incoming.ActivityEnabled,
            ChallengeEnabled = incoming.ChallengeEnabled,
            WeeklySummaryEnabled = incoming.WeeklySummaryEnabled,
            MealTime = incoming.MealTime,
            ActivityTime = incoming.ActivityTime,
            ChallengeTime = incoming.ChallengeTime,
            WeeklySummaryTime = incoming.WeeklySummaryTime,
            QuietStart = incoming.QuietStart,
            QuietEnd = incoming.QuietEnd,
            Version = incoming.Version,
        };
        var now = timeProvider.GetUtcNow();
        var schedules = await db.Schedules
            .Where(s => s.UserId == incoming.UserId).ToListAsync(cancellationToken);
        var changed = false;
        foreach (var category in Enum.GetValues<ReminderCategory>())
        {
            var row = schedules.SingleOrDefault(s => s.Category == category);
            if (row is null)
            {
                row = new ReminderSchedule { UserId = incoming.UserId, Category = category };
                db.Schedules.Add(row);
            }
            else if (row.PreferenceVersion >= incoming.Version)
            {
                continue; // out-of-order preference event cannot overwrite newer state
            }
            var enabled = pref.OptedIn && category switch
            {
                ReminderCategory.MealLogging => pref.MealLoggingEnabled,
                ReminderCategory.Activity => pref.ActivityEnabled,
                ReminderCategory.Challenge => pref.ChallengeEnabled,
                ReminderCategory.WeeklySummary => pref.WeeklySummaryEnabled,
                _ => false,
            };
            row.Enabled = enabled;
            row.TimeZone = zone.Id;
            row.LocalTime = TimeFor(pref, category);
            row.QuietStart = pref.QuietStart;
            row.QuietEnd = pref.QuietEnd;
            row.PreferenceVersion = incoming.Version;
            row.NextDueUtc = enabled ? ReminderScheduleCalculator.Next(
                category, row.LocalTime, pref, zone, now).DueAtUtc.UtcDateTime : null;
            row.UpdatedAtUtc = now.UtcDateTime;
            changed = true;
        }

        db.ProcessedEvents.Add(new WorkerProcessedEvent
        {
            EventId = incoming.EventId,
            ProcessedAtUtc = now.UtcDateTime,
        });
        await db.SaveChangesAsync(cancellationToken);
        return changed;
    }

    public async Task<int> ScheduleDueAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var candidates = await db.Schedules.AsNoTracking()
            .Where(s => s.Enabled && s.NextDueUtc != null &&
                        s.NextDueUtc <= now.UtcDateTime)
            .OrderBy(s => s.NextDueUtc)
            .Take(50).ToArrayAsync(cancellationToken);
        var scheduled = 0;
        foreach (var candidate in candidates)
        {
            // One transaction per due window; unique key guarantees one durable
            // dispatch even after worker restart or concurrent workers.
            try
            {
                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                var live = await db.Schedules.SingleOrDefaultAsync(
                    s => s.Id == candidate.Id, cancellationToken);
                if (live is null || !live.Enabled || live.NextDueUtc is null ||
                    live.NextDueUtc > now.UtcDateTime)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    continue;
                }

                var due = live.NextDueUtc.Value;
                var key = $"{live.UserId:N}:{live.Category}:{due:yyyyMMddHHmm}";
                var exists = await db.DispatchRecords.AsNoTracking()
                    .AnyAsync(r => r.DeduplicationKey == key, cancellationToken);
                var (title, body, deepLink) = ContentFor(live.Category);
                if (!exists)
                {
                    db.DispatchRecords.Add(new ReminderDispatchRecord
                    {
                        UserId = live.UserId,
                        Category = live.Category,
                        DueAtUtc = due,
                        DeduplicationKey = key,
                        CreatedAtUtc = now.UtcDateTime,
                    });
                    var eventId = Guid.NewGuid();
                    var payload = JsonSerializer.Serialize(new
                    {
                        eventId,
                        userId = live.UserId,
                        category = live.Category,
                        title,
                        body,
                        deepLink,
                        deduplicationKey = key,
                    });
                    db.Outbox.Add(new WorkerOutboxEvent
                    {
                        EventId = eventId,
                        EventType = "NotificationReady.v1",
                        PayloadJson = payload,
                        CreatedAtUtc = now.UtcDateTime,
                    });
                }

                var preference = new ReminderPreference
                {
                    UserId = live.UserId,
                    OptedIn = true,
                    MealLoggingEnabled = true,
                    ActivityEnabled = true,
                    ChallengeEnabled = true,
                    WeeklySummaryEnabled = true,
                    MealTime = live.LocalTime,
                    ActivityTime = live.LocalTime,
                    ChallengeTime = live.LocalTime,
                    WeeklySummaryTime = live.LocalTime,
                    QuietStart = live.QuietStart,
                    QuietEnd = live.QuietEnd,
                };
                var zone = TimeZoneInfo.FindSystemTimeZoneById(live.TimeZone);
                live.NextDueUtc = ReminderScheduleCalculator.Next(
                    live.Category, live.LocalTime, preference, zone, now).DueAtUtc.UtcDateTime;
                live.UpdatedAtUtc = now.UtcDateTime;
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                if (!exists) scheduled++;
            }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
                // Unique dispatch may have been committed by another worker.
                // Source event will be rechecked on next iteration.
            }
        }
        return scheduled;
    }

    private static TimeOnly TimeFor(ReminderPreference preference, ReminderCategory category) =>
        category switch
        {
            ReminderCategory.MealLogging => preference.MealTime,
            ReminderCategory.Activity => preference.ActivityTime,
            ReminderCategory.Challenge => preference.ChallengeTime,
            ReminderCategory.WeeklySummary => preference.WeeklySummaryTime,
            _ => throw new ArgumentOutOfRangeException(nameof(category)),
        };

    private static (string Title, string Body, string DeepLink) ContentFor(ReminderCategory category) =>
        category switch
        {
            ReminderCategory.MealLogging =>
                ("Ghi nhận bữa ăn", "Bạn có thể ghi lại các bữa ăn hôm nay khi thuận tiện.", "/app"),
            ReminderCategory.Activity =>
                ("Ghi nhận vận động", "Hãy ghi lại hoạt động bạn đã thực hiện hôm nay.", "/activity"),
            ReminderCategory.Challenge =>
                ("Theo dõi thử thách", "Kiểm tra tiến độ thử thách theo nhịp của bạn.", "/app"),
            ReminderCategory.WeeklySummary =>
                ("Tổng kết tuần", "Xem lại những hoạt động đã ghi nhận trong tuần.", "/activity"),
            _ => throw new ArgumentOutOfRangeException(nameof(category)),
        };
}
