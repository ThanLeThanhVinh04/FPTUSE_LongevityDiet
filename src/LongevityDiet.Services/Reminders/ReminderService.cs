using LongevityDiet.Domain.Entities;
using LongevityDiet.Repositories;
using LongevityDiet.Repositories.Repositories;
using Microsoft.EntityFrameworkCore;

namespace LongevityDiet.Services.Reminders;

public sealed record ReminderPreferencesInput(
    bool OptedIn, bool MealLoggingEnabled, bool ActivityEnabled,
    bool ChallengeEnabled, bool WeeklySummaryEnabled,
    TimeOnly MealTime, TimeOnly ActivityTime, TimeOnly ChallengeTime,
    TimeOnly WeeklySummaryTime, TimeOnly QuietStart, TimeOnly QuietEnd);
public sealed record ReminderPreferencesView(
    bool OptedIn, bool MealLoggingEnabled, bool ActivityEnabled,
    bool ChallengeEnabled, bool WeeklySummaryEnabled,
    TimeOnly MealTime, TimeOnly ActivityTime, TimeOnly ChallengeTime,
    TimeOnly WeeklySummaryTime, TimeOnly QuietStart, TimeOnly QuietEnd,
    int Version, string TimeZone);
public sealed record NotificationView(
    Guid Id, ReminderCategory Category, string Title, string Body,
    string DeepLink, DateTime CreatedAtUtc, DateTime? ReadAtUtc);
public sealed record NotificationPage(
    IReadOnlyList<NotificationView> Items, int Page, int PageSize,
    int TotalItems, int TotalPages);
public sealed record NotificationReadyEvent(
    Guid EventId, Guid UserId, ReminderCategory Category,
    string Title, string Body, string DeepLink, string DeduplicationKey);

public sealed class ReminderAccessException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class ReminderService(
    LongevityDietDbContext db,
    IUserRepository users,
    TimeProvider clock)
{
    private async Task<User> ActiveUserAsync(Guid userId, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId, ct);
        if (user is not { Status: AccountStatus.Active })
            throw new ReminderAccessException("inactive_user", "Active account is required.");
        return user;
    }

    public async Task<ReminderPreferencesView> GetPreferencesAsync(Guid userId, CancellationToken ct)
    {
        var user = await ActiveUserAsync(userId, ct);
        var pref = await db.ReminderPreferences.AsNoTracking()
            .SingleOrDefaultAsync(p => p.UserId == userId, ct)
            ?? new ReminderPreference { UserId = userId };
        return Project(pref, user.Profile?.TimeZone ?? "Asia/Ho_Chi_Minh");
    }

    public async Task<ReminderPreferencesView> UpdatePreferencesAsync(
        Guid userId, ReminderPreferencesInput request, CancellationToken ct)
    {
        var user = await ActiveUserAsync(userId, ct);
        var pref = await db.ReminderPreferences.SingleOrDefaultAsync(p => p.UserId == userId, ct);
        if (pref is null)
        {
            pref = new ReminderPreference { UserId = userId };
            db.ReminderPreferences.Add(pref);
        }
        else
        {
            pref.Version++;
        }
        pref.OptedIn = request.OptedIn;
        pref.MealLoggingEnabled = request.MealLoggingEnabled;
        pref.ActivityEnabled = request.ActivityEnabled;
        pref.ChallengeEnabled = request.ChallengeEnabled;
        pref.WeeklySummaryEnabled = request.WeeklySummaryEnabled;
        pref.MealTime = request.MealTime;
        pref.ActivityTime = request.ActivityTime;
        pref.ChallengeTime = request.ChallengeTime;
        pref.WeeklySummaryTime = request.WeeklySummaryTime;
        pref.QuietStart = request.QuietStart;
        pref.QuietEnd = request.QuietEnd;
        pref.UpdatedAtUtc = clock.GetUtcNow();

        var zone = user.Profile?.TimeZone ?? "Asia/Ho_Chi_Minh";
        db.IdentityOutbox.Add(ReminderPreferenceEventFactory.Create(
            userId, zone, true, pref, clock.GetUtcNow()));
        // Preference + outgoing event committed in ONE identity DB transaction.
        await db.SaveChangesAsync(ct);
        return Project(pref, zone);
    }

    public async Task<IReadOnlyList<ReminderScheduleItem>> PreviewAsync(Guid userId, CancellationToken ct)
    {
        var user = await ActiveUserAsync(userId, ct);
        var pref = await db.ReminderPreferences.AsNoTracking()
            .SingleOrDefaultAsync(p => p.UserId == userId, ct)
            ?? new ReminderPreference { UserId = userId };
        return ReminderScheduleCalculator.Preview(pref,
            user.Profile?.TimeZone ?? "Asia/Ho_Chi_Minh", clock.GetUtcNow());
    }

    public async Task<NotificationPage> ListAsync(Guid userId, int page, int pageSize, CancellationToken ct)
    {
        await ActiveUserAsync(userId, ct);
        ValidatePage(page, pageSize);
        var query = db.Notifications.AsNoTracking().Where(n => n.UserId == userId);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(n => n.CreatedAtUtc).ThenBy(n => n.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(n => new NotificationView(
                n.Id, n.Category, n.Title, n.Body, n.DeepLink, n.CreatedAtUtc, n.ReadAtUtc))
            .ToArrayAsync(ct);
        return new NotificationPage(items, page, pageSize,
            total, (total + pageSize - 1) / pageSize);
    }

    public async Task<int> UnreadCountAsync(Guid userId, CancellationToken ct)
    {
        await ActiveUserAsync(userId, ct);
        return await db.Notifications.AsNoTracking()
            .CountAsync(n => n.UserId == userId && n.ReadAtUtc == null, ct);
    }

    public async Task<bool> MarkReadAsync(Guid userId, Guid id, CancellationToken ct)
    {
        await ActiveUserAsync(userId, ct);
        var notification = await db.Notifications.SingleOrDefaultAsync(
            n => n.UserId == userId && n.Id == id, ct);
        if (notification is null) return false;
        if (notification.ReadAtUtc is null)
        {
            notification.ReadAtUtc = clock.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(ct);
        }
        return true;
    }

    public async Task<int> MarkAllReadAsync(Guid userId, CancellationToken ct)
    {
        await ActiveUserAsync(userId, ct);
        var records = await db.Notifications.Where(
            n => n.UserId == userId && n.ReadAtUtc == null).ToListAsync(ct);
        foreach (var item in records) item.ReadAtUtc = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        return records.Count;
    }

    /// <summary>Called only by the authenticated Redis notification consumer.</summary>
    public async Task<bool> PersistNotificationResultAsync(NotificationReadyEvent incoming, CancellationToken ct)
    {
        if (incoming.UserId == Guid.Empty || incoming.EventId == Guid.Empty ||
            string.IsNullOrWhiteSpace(incoming.DeduplicationKey) ||
            incoming.DeduplicationKey.Length > 160 ||
            incoming.Title.Length is < 1 or > 120 ||
            incoming.Body.Length > 500 ||
            !Enum.IsDefined(incoming.Category) ||
            incoming.DeepLink is not ("/app" or "/activity" or "/notifications"))
            throw new ReminderAccessException("invalid_notification_event", "Invalid trusted notification event.");

        // SQL Server's retrying execution strategy cannot run a manually-created
        // transaction unless the ENTIRE unit of work is wrapped in ExecuteAsync.
        // On retries, discard previously tracked changes before recomputing.
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            if (await db.IdentityInbox.AnyAsync(x => x.EventId == incoming.EventId, ct))
                return false;

            // Recheck current consent and account state, never trust worker snapshots.
            var user = await users.FindByIdAsync(incoming.UserId, ct);
            var pref = await db.ReminderPreferences.AsNoTracking()
                .SingleOrDefaultAsync(p => p.UserId == incoming.UserId, ct);
            var enabledForCategory = pref is { OptedIn: true } && incoming.Category switch
            {
                ReminderCategory.MealLogging => pref.MealLoggingEnabled,
                ReminderCategory.Activity => pref.ActivityEnabled,
                ReminderCategory.Challenge => pref.ChallengeEnabled,
                ReminderCategory.WeeklySummary => pref.WeeklySummaryEnabled,
                _ => false,
            };
            var exists = await db.Notifications.AsNoTracking().AnyAsync(
                n => n.UserId == incoming.UserId &&
                     n.DeduplicationKey == incoming.DeduplicationKey, ct);

            if (user is { Status: AccountStatus.Active } && enabledForCategory && !exists)
            {
                db.Notifications.Add(new UserNotification
                {
                    UserId = incoming.UserId,
                    Category = incoming.Category,
                    Title = incoming.Title,
                    Body = incoming.Body,
                    DeepLink = incoming.DeepLink,
                    DeduplicationKey = incoming.DeduplicationKey,
                    CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
                });
            }
            db.IdentityInbox.Add(new IdentityInboxEvent
            {
                EventId = incoming.EventId,
                ReceivedAtUtc = clock.GetUtcNow().UtcDateTime,
            });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return !exists && enabledForCategory && user is { Status: AccountStatus.Active };
        });
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page is < 1 or > 100000 || pageSize is < 1 or > 100)
            throw new ReminderAccessException("invalid_pagination", "Page must be 1–100000 and pageSize 1–100.");
    }

    private static ReminderPreferencesView Project(ReminderPreference p, string timeZone) =>
        new(p.OptedIn, p.MealLoggingEnabled, p.ActivityEnabled, p.ChallengeEnabled,
            p.WeeklySummaryEnabled, p.MealTime, p.ActivityTime, p.ChallengeTime,
            p.WeeklySummaryTime, p.QuietStart, p.QuietEnd, p.Version, timeZone);
}
