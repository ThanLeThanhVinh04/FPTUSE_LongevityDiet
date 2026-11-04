using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace LongevityDiet.Tracking;

public interface ITrackingUserContext
{
    Task<string?> GetActiveUserTimeZoneAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed record ActivityInput(
    ActivityKind Kind, DateTimeOffset StartedAt, int DurationMinutes, string? Note);
public sealed record ActivityView(
    Guid Id, ActivityKind Kind, DateTimeOffset StartedAtUtc,
    int DurationMinutes, string? Note);
public sealed record PageView<T>(
    IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems, int TotalPages);
public sealed record GoalInput(int WeeklyMinutesTarget, int WeeklyStrengthSessionsTarget);
public sealed record GoalView(int WeeklyMinutesTarget, int WeeklyStrengthSessionsTarget, DateTimeOffset UpdatedAtUtc);
public sealed record ActivitySummary(
    DateOnly WeekStart, DateOnly WeekEnd, string TimeZone,
    int EquivalentMinutes, int StrengthSessions, int WeeklyMinutesTarget,
    int WeeklyStrengthSessionsTarget, decimal MinutesProgressPercent,
    decimal StrengthProgressPercent);
public sealed record ScoreView(
    DateOnly LocalDate, string Version, decimal? Score, decimal CoveragePercent,
    bool IsPartial, IReadOnlyList<string> MissingData,
    IReadOnlyList<DimensionValue> Breakdown, bool IsClinical,
    string Disclaimer, DateTimeOffset CalculatedAtUtc);

public sealed class TrackingInputException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class TrackingService(
    TrackingDbContext db,
    ITrackingUserContext userContext,
    TimeProvider clock)
{
    public async Task<ActivityView> CreateAsync(Guid userId, ActivityInput input, CancellationToken ct)
    {
        var zone = await ReadZoneAsync(userId, ct);
        Validate(input);
        var now = clock.GetUtcNow();
        var entity = new ActivityLog
        {
            UserId = userId,
            Kind = input.Kind,
            StartedAtUtc = input.StartedAt.UtcDateTime,
            DurationMinutes = input.DurationMinutes,
            Note = input.Note?.Trim(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        db.Activities.Add(entity);
        QueueActivityEvent(userId, entity, "created");
        await db.SaveChangesAsync(ct);
        await RecalculateAsync(userId, LocalDate(entity.StartedAtUtc, zone), zone, ct);
        return Project(entity);
    }

    public async Task<ActivityView?> UpdateAsync(Guid userId, Guid id, ActivityInput input, CancellationToken ct)
    {
        var zone = await ReadZoneAsync(userId, ct);
        Validate(input);
        var entity = await db.Activities.SingleOrDefaultAsync(
            a => a.UserId == userId && a.Id == id && !a.IsDeleted, ct);
        if (entity is null) return null;

        var oldDate = LocalDate(entity.StartedAtUtc, zone);
        entity.Kind = input.Kind;
        entity.StartedAtUtc = input.StartedAt.UtcDateTime;
        entity.DurationMinutes = input.DurationMinutes;
        entity.Note = input.Note?.Trim();
        entity.UpdatedAtUtc = clock.GetUtcNow();
        QueueActivityEvent(userId, entity, "updated");
        await db.SaveChangesAsync(ct);

        await RecalculateAsync(userId, oldDate, zone, ct);
        var newDate = LocalDate(entity.StartedAtUtc, zone);
        if (newDate != oldDate) await RecalculateAsync(userId, newDate, zone, ct);
        return Project(entity);
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var zone = await ReadZoneAsync(userId, ct);
        var entity = await db.Activities.SingleOrDefaultAsync(
            a => a.UserId == userId && a.Id == id && !a.IsDeleted, ct);
        if (entity is null) return false;
        entity.IsDeleted = true;
        entity.UpdatedAtUtc = clock.GetUtcNow();
        QueueActivityEvent(userId, entity, "deleted");
        await db.SaveChangesAsync(ct);
        await RecalculateAsync(userId, LocalDate(entity.StartedAtUtc, zone), zone, ct);
        return true;
    }

    public async Task<PageView<ActivityView>> ListAsync(
        Guid userId, DateTimeOffset? from, DateTimeOffset? to,
        int page, int pageSize, CancellationToken ct)
    {
        await ReadZoneAsync(userId, ct);
        ValidatePage(page, pageSize);
        if (from is not null && to is not null && from > to)
            throw new TrackingInputException("invalid_date_range", "From must precede To.");
        var query = db.Activities.AsNoTracking().Where(a => a.UserId == userId && !a.IsDeleted);
        if (from is not null) query = query.Where(a => a.StartedAtUtc >= from.Value.UtcDateTime);
        if (to is not null) query = query.Where(a => a.StartedAtUtc <= to.Value.UtcDateTime);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(a => a.StartedAtUtc).ThenBy(a => a.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new ActivityView(a.Id, a.Kind, a.StartedAtUtc, a.DurationMinutes, a.Note))
            .ToListAsync(ct);
        return new PageView<ActivityView>(items, page, pageSize, total,
            (total + pageSize - 1) / pageSize);
    }

    public async Task<GoalView> GetGoalAsync(Guid userId, CancellationToken ct)
    {
        await ReadZoneAsync(userId, ct);
        var goal = await db.Goals.AsNoTracking().SingleOrDefaultAsync(g => g.UserId == userId, ct);
        return goal is null ? new GoalView(150, 2, DateTimeOffset.MinValue) : Project(goal);
    }

    public async Task<GoalView> SetGoalAsync(Guid userId, GoalInput input, CancellationToken ct)
    {
        var zone = await ReadZoneAsync(userId, ct);
        if (input.WeeklyMinutesTarget is < 1 or > 1000 ||
            input.WeeklyStrengthSessionsTarget is < 0 or > 7)
            throw new TrackingInputException("invalid_goal", "Weekly minutes must be 1–1000; strength sessions 0–7.");
        var goal = await db.Goals.SingleOrDefaultAsync(g => g.UserId == userId, ct);
        if (goal is null)
        {
            goal = new ActivityGoal { UserId = userId };
            db.Goals.Add(goal);
        }
        goal.WeeklyMinutesTarget = input.WeeklyMinutesTarget;
        goal.WeeklyStrengthSessionsTarget = input.WeeklyStrengthSessionsTarget;
        goal.UpdatedAtUtc = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        // Scoring input changed; invalidate/recalculate recent activity observations.
        var today = LocalDate(clock.GetUtcNow(), zone);
        for (var i = 0; i < 7; i++)
            await RecalculateAsync(userId, today.AddDays(-i), zone, ct);
        return Project(goal);
    }

    public async Task<ActivitySummary> GetWeeklySummaryAsync(
        Guid userId, DateOnly? weekStart, CancellationToken ct)
    {
        var zone = await ReadZoneAsync(userId, ct);
        var today = LocalDate(clock.GetUtcNow(), zone);
        var start = weekStart ?? today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        if (start.DayOfWeek != DayOfWeek.Monday)
            throw new TrackingInputException("invalid_week_start", "Week start must be a Monday.");
        var (utcStart, utcEnd) = UtcBounds(start, start.AddDays(7), zone);
        var activities = await db.Activities.AsNoTracking()
            .Where(a => a.UserId == userId && !a.IsDeleted &&
                        a.StartedAtUtc >= utcStart.UtcDateTime && a.StartedAtUtc < utcEnd.UtcDateTime)
            .ToListAsync(ct);
        var goal = await GoalEntityAsync(userId, ct);
        var total = activities.Sum(a => a.DurationMinutes *
            (a.Kind == ActivityKind.Vigorous ? 2 : 1));
        var strength = activities.Count(a => a.Kind == ActivityKind.Strength);
        return new ActivitySummary(
            start, start.AddDays(6), zone.Id, total, strength,
            goal.WeeklyMinutesTarget, goal.WeeklyStrengthSessionsTarget,
            Math.Round(Math.Min(100m, total * 100m / goal.WeeklyMinutesTarget), 2),
            goal.WeeklyStrengthSessionsTarget == 0 ? 100m :
                Math.Round(Math.Min(100m, strength * 100m / goal.WeeklyStrengthSessionsTarget), 2));
    }

    public async Task<ScoreView> GetScoreAsync(Guid userId, DateOnly date, CancellationToken ct)
    {
        var zone = await ReadZoneAsync(userId, ct);
        if (date > LocalDate(clock.GetUtcNow(), zone))
            throw new TrackingInputException("future_score_date", "Cannot score a future day.");
        return await RecalculateAsync(userId, date, zone, ct);
    }

    public async Task<PageView<ScoreView>> HistoryAsync(
        Guid userId, int page, int pageSize, CancellationToken ct)
    {
        await ReadZoneAsync(userId, ct);
        ValidatePage(page, pageSize);
        var q = db.Scores.AsNoTracking().Where(s => s.UserId == userId);
        var total = await q.CountAsync(ct);
        var scores = await q.Include(s => s.Dimensions)
            .OrderByDescending(s => s.LocalDate).ThenBy(s => s.Version)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PageView<ScoreView>(scores.Select(Project).ToArray(),
            page, pageSize, total, (total + pageSize - 1) / pageSize);
    }

    private async Task<ScoreView> RecalculateAsync(
        Guid userId, DateOnly date, TimeZoneInfo zone, CancellationToken ct)
    {
        var config = await db.ScoreConfigurations.AsNoTracking().SingleAsync(
            v => v.Version == "ldas-v1" && v.Published, ct);
        var goal = await GoalEntityAsync(userId, ct);
        var (start, end) = UtcBounds(date, date.AddDays(1), zone);
        var activities = await db.Activities.AsNoTracking()
            .Where(a => a.UserId == userId && !a.IsDeleted &&
                        a.StartedAtUtc >= start.UtcDateTime && a.StartedAtUtc < end.UtcDateTime)
            .ToListAsync(ct);
        var signals = await db.ScoreSignals.AsNoTracking()
            .Where(s => s.UserId == userId && s.LocalDate == date).ToListAsync(ct);
        decimal? meal = signals.SingleOrDefault(s => s.DimensionCode == "meals")?.Percent;
        decimal? window = signals.SingleOrDefault(s => s.DimensionCode == "eating_window")?.Percent;
        var outcome = LdasCalculator.Calculate(config,
            LdasCalculator.ActivityPercent(activities, goal), meal, window);

        var score = await db.Scores.Include(s => s.Dimensions).SingleOrDefaultAsync(
            s => s.UserId == userId && s.LocalDate == date && s.Version == config.Version, ct);
        if (score is null)
        {
            score = new AdherenceScore
            {
                UserId = userId, LocalDate = date, Version = config.Version,
            };
            db.Scores.Add(score);
        }
        score.Score = outcome.Score;
        score.CoveragePercent = outcome.CoveragePercent;
        score.IsPartial = outcome.IsPartial;
        score.MissingDataJson = LdasCalculator.MissingAsJson(outcome);
        score.CalculatedAtUtc = clock.GetUtcNow();
        // Update the stable dimension rows in-place to preserve identities
        // and avoid orphan deletes/insert races on unique (ScoreId, Code).
        foreach (var dimension in outcome.Breakdown)
        {
            var row = score.Dimensions.SingleOrDefault(d => d.Code == dimension.Code);
            if (row is null)
            {
                row = new ScoreDimension { Code = dimension.Code };
                score.Dimensions.Add(row);
            }
            row.Weight = dimension.Weight;
            row.RawScore = dimension.RawScore;
            row.WeightedScore = dimension.WeightedScore;
            row.ReasonCode = dimension.ReasonCode;
        }
        await db.SaveChangesAsync(ct);
        return Project(score);
    }

    private async Task<ActivityGoal> GoalEntityAsync(Guid userId, CancellationToken ct) =>
        await db.Goals.AsNoTracking().SingleOrDefaultAsync(g => g.UserId == userId, ct)
        ?? new ActivityGoal { UserId = userId };

    private async Task<TimeZoneInfo> ReadZoneAsync(Guid userId, CancellationToken ct)
    {
        var zoneName = await userContext.GetActiveUserTimeZoneAsync(userId, ct);
        if (string.IsNullOrWhiteSpace(zoneName))
            throw new TrackingInputException("inactive_user", "Active member account is required.");
        try { return TimeZoneInfo.FindSystemTimeZoneById(zoneName); }
        catch (TimeZoneNotFoundException) { throw new TrackingInputException("invalid_time_zone", "Invalid profile time zone."); }
        catch (InvalidTimeZoneException) { throw new TrackingInputException("invalid_time_zone", "Invalid profile time zone."); }
    }

    public static DateOnly LocalDate(DateTimeOffset utc, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utc, zone).DateTime);

    public static (DateTimeOffset Start, DateTimeOffset End) UtcBounds(
        DateOnly start, DateOnly endExclusive, TimeZoneInfo zone)
    {
        var localStart = start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var localEnd = endExclusive.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return (new DateTimeOffset(localStart, zone.GetUtcOffset(localStart)).ToUniversalTime(),
            new DateTimeOffset(localEnd, zone.GetUtcOffset(localEnd)).ToUniversalTime());
    }

    private void Validate(ActivityInput input)
    {
        if (!Enum.IsDefined(input.Kind))
            throw new TrackingInputException("invalid_activity_type", "Unknown activity type.");
        if (input.DurationMinutes is < 1 or > 720)
            throw new TrackingInputException("invalid_duration", "Duration must be 1–720 minutes.");
        if (input.Note?.Length > 800)
            throw new TrackingInputException("invalid_note", "Note cannot exceed 800 characters.");
        var now = clock.GetUtcNow();
        if (input.StartedAt.ToUniversalTime() < now.AddDays(-366) ||
            input.StartedAt.ToUniversalTime() > now.AddDays(1))
            throw new TrackingInputException("invalid_start_time", "Activity time is outside the supported range.");
    }

    private static void ValidatePage(int page, int size)
    {
        if (page is < 1 or > 100000 || size is < 1 or > 100)
            throw new TrackingInputException("invalid_pagination", "Page must be 1–100000 and pageSize 1–100.");
    }

    private void QueueActivityEvent(Guid userId, ActivityLog activity, string operation)
    {
        var eventId = Guid.NewGuid();
        db.Outbox.Add(new TrackingOutboxEvent
        {
            EventId = eventId,
            UserId = userId,
            EventType = operation == "deleted" ? "ActivityDeleted.v1" : "ActivityLogged.v1",
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
            PayloadJson = JsonSerializer.Serialize(new
            {
                eventId,
                userId,
                activityId = activity.Id,
                operation,
                activityKind = activity.Kind,
                durationMinutes = activity.DurationMinutes,
                startedAtUtc = activity.StartedAtUtc,
            }),
        });
    }

    private static ActivityView Project(ActivityLog a) =>
        new(a.Id, a.Kind, a.StartedAtUtc, a.DurationMinutes, a.Note);
    private static GoalView Project(ActivityGoal a) =>
        new(a.WeeklyMinutesTarget, a.WeeklyStrengthSessionsTarget, a.UpdatedAtUtc);
    private static ScoreView Project(AdherenceScore s)
    {
        var breakdown = s.Dimensions.Select(d =>
            new DimensionValue(d.Code, d.Weight, d.RawScore, d.WeightedScore, d.ReasonCode)).ToArray();
        var missing = System.Text.Json.JsonSerializer.Deserialize<List<string>>(s.MissingDataJson)
            ?? [];
        return new ScoreView(s.LocalDate, s.Version, s.Score, s.CoveragePercent,
            s.IsPartial, missing, breakdown, false,
            "LDAS is an educational habit indicator, not a diagnosis or longevity prediction.",
            s.CalculatedAtUtc);
    }
}
