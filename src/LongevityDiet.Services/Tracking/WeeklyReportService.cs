using LongevityDiet.Domain.Entities;
using LongevityDiet.Repositories.Repositories;
using LongevityDiet.Services.Identity;

namespace LongevityDiet.Services.Tracking;

public sealed class WeeklyReportService(
    IWeeklyReportWorkerRepository repository,
    IChallengeProgressRepository progressRepository,
    ITrackingUserScheduleProvider scheduleProvider,
    TimeProvider timeProvider)
{
    public const int CurrentReportVersion = 1;

    public async Task<ServiceResult<WeeklyReportSummary>> GenerateAsync(
        Guid userId, DateOnly weekStart, CancellationToken cancellationToken = default)
    {
        var normalizedStart = StartOfWeek(weekStart);
        var existing = await repository.GetReportByWeekAsync(userId, normalizedStart, CurrentReportVersion, cancellationToken);
        if (existing is not null) return ServiceResult.Success(ToSummary(existing));

        var schedule = await scheduleProvider.GetUserScheduleAsync(userId, cancellationToken);
        if (schedule is null)
            return ServiceResult.Failure<WeeklyReportSummary>("user_not_found", "User was not found.");

        var weekEnd = normalizedStart.AddDays(6);
        var days = await progressRepository.ListDailySummariesAsync(userId, normalizedStart, weekEnd, cancellationToken);
        var totalDays = 7;
        var loggedDays = days.Count(x => x.LoggedMealCount > 0);
        var completedChallengeDays = days.Count(x => x.ChallengeDayCompleted);
        var targetWindowDays = days.Count(x => x.EatingWindowWithinTarget == true);
        var observedWindowDays = days.Count(x => x.EatingWindowWithinTarget.HasValue);
        var averageCalories = days.Where(x => x.LoggedMealCount > 0).Select(x => (decimal)x.TotalCaloriesKcal).DefaultIfEmpty(0m).Average();
        var latestStreak = days.OrderByDescending(x => x.LocalDate).Select(x => x.ChallengeCurrentStreak).FirstOrDefault();

        var report = new WeeklyReport
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            WeekStart = normalizedStart,
            WeekEnd = weekEnd,
            ReportVersion = CurrentReportVersion,
            Status = WeeklyReportStatus.Ready,
            TimeZone = schedule.TimeZone,
            Summary = loggedDays == 0
                ? "Chưa có dữ liệu ghi nhận trong tuần này. Báo cáo không suy đoán dữ liệu còn thiếu."
                : $"Bạn đã ghi nhận dữ liệu {loggedDays}/7 ngày; {completedChallengeDays} ngày challenge được hoàn thành.",
            GeneratedAt = timeProvider.GetUtcNow(),
        };

        report.Metrics.Add(Metric(report.Id, "logging_consistency", loggedDays * 100m / totalDays, null, "%"));
        report.Metrics.Add(Metric(report.Id, "average_calories", decimal.Round(averageCalories, 1), null, "kcal"));
        report.Metrics.Add(Metric(report.Id, "challenge_completed_days", completedChallengeDays, null, "days"));
        report.Metrics.Add(Metric(report.Id, "challenge_streak", latestStreak, null, "days"));
        report.Metrics.Add(Metric(report.Id, "eating_window_target_days", targetWindowDays, null, "days"));
        report.Metrics.Add(Metric(report.Id, "eating_window_observed_days", observedWindowDays, null, "days"));
        report.Metrics.Add(Metric(report.Id, "activity", null, "unavailable:no-activity-source", ""));
        report.Metrics.Add(Metric(report.Id, "ldas", null, "unavailable:no-ldas-source", ""));

        await repository.AddReportAsync(report, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return ServiceResult.Success(ToSummary(report));
    }

    public async Task<ServiceResult<IReadOnlyList<WeeklyReportSummary>>> ListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var reports = await repository.ListReportsAsync(userId, cancellationToken);
        return ServiceResult.Success<IReadOnlyList<WeeklyReportSummary>>(reports.Select(ToSummary).ToList());
    }

    public async Task<ServiceResult<WeeklyReportSummary>> GetAsync(Guid userId, Guid reportId, CancellationToken cancellationToken = default)
    {
        var report = await repository.GetReportAsync(reportId, userId, cancellationToken);
        return report is null
            ? ServiceResult.Failure<WeeklyReportSummary>("weekly_report_not_found", "Weekly report was not found.")
            : ServiceResult.Success(ToSummary(report));
    }

    public async Task<ServiceResult<WeeklyReportSummary>> GetCurrentAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var week = await GetCurrentWeekStartAsync(userId, cancellationToken);
        if (!week.Succeeded || week.Value == default)
            return ServiceResult.Failure<WeeklyReportSummary>(week.ErrorCode ?? "user_not_found", week.ErrorMessage ?? "User was not found.");

        var report = await repository.GetReportByWeekAsync(userId, week.Value, CurrentReportVersion, cancellationToken);
        return report is null
            ? ServiceResult.Failure<WeeklyReportSummary>("weekly_report_pending", "Current weekly report has not been generated yet.")
            : ServiceResult.Success(ToSummary(report));
    }

    public async Task<ServiceResult<WeeklyReportSummary>> GetOrGenerateCurrentAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var current = await GetCurrentAsync(userId, cancellationToken);
        if (current.Succeeded) return current;

        var week = await GetCurrentWeekStartAsync(userId, cancellationToken);
        if (!week.Succeeded || week.Value == default)
            return ServiceResult.Failure<WeeklyReportSummary>(week.ErrorCode ?? "user_not_found", week.ErrorMessage ?? "User was not found.");
        return await GenerateAsync(userId, week.Value, cancellationToken);
    }

    public async Task<ServiceResult<DateOnly>> GetCurrentWeekStartAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var schedule = await scheduleProvider.GetUserScheduleAsync(userId, cancellationToken);
        if (schedule is null)
            return ServiceResult.Failure<DateOnly>("user_not_found", "User was not found.");

        var today = ToLocalDate(timeProvider.GetUtcNow(), schedule.TimeZone);
        return ServiceResult.Success(StartOfWeek(today));
    }

    public static DateOnly StartOfWeek(DateOnly date)
    {
        var delta = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-delta);
    }

    private static WeeklyReportMetric Metric(Guid reportId, string key, decimal? numeric, string? text, string unit) =>
        new() { Id = Guid.NewGuid(), WeeklyReportId = reportId, MetricKey = key, NumericValue = numeric, TextValue = text, Unit = unit };

    private static WeeklyReportSummary ToSummary(WeeklyReport report) =>
        new(report.Id, report.UserId, report.WeekStart, report.WeekEnd, report.ReportVersion,
            report.Status.ToString(), report.TimeZone, report.Summary, report.GeneratedAt,
            report.Metrics.OrderBy(x => x.MetricKey)
                .Select(x => new WeeklyReportMetricSummary(x.MetricKey, x.NumericValue, x.TextValue, x.Unit)).ToList());

    private static DateOnly ToLocalDate(DateTimeOffset instant, string timeZoneId)
    {
        try { return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, TimeZoneInfo.FindSystemTimeZoneById(timeZoneId)).DateTime); }
        catch (TimeZoneNotFoundException) { return DateOnly.FromDateTime(instant.UtcDateTime); }
        catch (InvalidTimeZoneException) { return DateOnly.FromDateTime(instant.UtcDateTime); }
    }
}
