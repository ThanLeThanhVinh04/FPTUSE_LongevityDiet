using LongevityDiet.Repositories;
using LongevityDiet.Services.Tracking;
using Microsoft.EntityFrameworkCore;

namespace LongevityDiet.Worker;

public sealed class WeeklyReportWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<WeeklyReportWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, string, Exception?> WorkerStarted =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(1001, nameof(WorkerStarted)),
            "Weekly report worker {InstanceId} started.");
    private static readonly Action<ILogger, int, Exception?> JobsProcessed =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(1002, nameof(JobsProcessed)),
            "Processed {Count} worker jobs.");
    private static readonly Action<ILogger, Exception?> WorkerLoopFailed =
        LoggerMessage.Define(LogLevel.Error, new EventId(1003, nameof(WorkerLoopFailed)),
            "Worker loop failed; retrying on next bounded poll.");
    private static readonly Action<ILogger, string, Exception?> WorkerStopped =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(1004, nameof(WorkerStopped)),
            "Weekly report worker {InstanceId} stopped.");

    private readonly string _instanceId = $"{Environment.MachineName}-{Guid.NewGuid():N}";
    private DateTimeOffset _nextScheduleScan = DateTimeOffset.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        WorkerStarted(logger, _instanceId, null);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));

        try
        {
            do
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var service = scope.ServiceProvider.GetRequiredService<WorkerReliabilityService>();
                    await service.HeartbeatAsync(_instanceId, stoppingToken);
                    await service.RecoverStaleProcessingJobsAsync(TimeSpan.FromMinutes(2), stoppingToken);

                    var now = DateTimeOffset.UtcNow;
                    if (now >= _nextScheduleScan)
                    {
                        var db = scope.ServiceProvider.GetRequiredService<LongevityDietDbContext>();
                        var reportService = scope.ServiceProvider.GetRequiredService<WeeklyReportService>();
                        var userIds = await db.Users.AsNoTracking().Select(x => x.Id).ToListAsync(stoppingToken);
                        foreach (var userId in userIds)
                        {
                            var week = await reportService.GetCurrentWeekStartAsync(userId, stoppingToken);
                            if (week.Succeeded && week.Value != default)
                                await service.ScheduleWeeklyReportAsync(userId, week.Value, stoppingToken);
                        }
                        _nextScheduleScan = now.AddHours(1);
                    }

                    var count = await service.ProcessRunnableJobsAsync(_instanceId, 10, stoppingToken);
                    if (count > 0) JobsProcessed(logger, count, null);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    WorkerLoopFailed(logger, ex);
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }

        WorkerStopped(logger, _instanceId, null);
    }
}
