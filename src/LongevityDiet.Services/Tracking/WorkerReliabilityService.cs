using LongevityDiet.Domain.Entities;
using LongevityDiet.Repositories.Repositories;
using LongevityDiet.Services.Identity;

namespace LongevityDiet.Services.Tracking;

public sealed class WorkerReliabilityService(
    IWeeklyReportWorkerRepository repository,
    WeeklyReportService weeklyReportService,
    TimeProvider timeProvider)
{
    public async Task<ServiceResult<WorkerJobSummary>> ScheduleWeeklyReportAsync(
        Guid userId, DateOnly weekStart, CancellationToken cancellationToken = default)
    {
        var normalized = WeeklyReportService.StartOfWeek(weekStart);
        var key = $"weekly-report:{userId:N}:{normalized:yyyyMMdd}:v{WeeklyReportService.CurrentReportVersion}";
        var existing = await repository.GetJobByKeyAsync(key, cancellationToken);
        if (existing is not null) return ServiceResult.Success(ToSummary(existing));

        var now = timeProvider.GetUtcNow();
        var job = new WorkerJob
        {
            Id = Guid.NewGuid(), JobType = "WeeklyReport", IdempotencyKey = key,
            UserId = userId, PeriodStart = normalized, MaxAttempts = 5,
            Status = WorkerJobStatus.Pending, NextAttemptAt = now, CreatedAt = now, UpdatedAt = now,
        };
        await repository.AddJobAsync(job, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return ServiceResult.Success(ToSummary(job));
    }

    public async Task<int> ProcessRunnableJobsAsync(string instanceId, int limit = 10, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var jobs = await repository.ListRunnableJobsAsync(now, Math.Clamp(limit, 1, 50), cancellationToken);
        var processed = 0;
        foreach (var job in jobs)
        {
            if (cancellationToken.IsCancellationRequested) break;
            job.Status = WorkerJobStatus.Processing;
            job.LockedBy = instanceId;
            job.LockedAt = now;
            job.UpdatedAt = now;
            await repository.SaveChangesAsync(cancellationToken);

            try
            {
                if (job.JobType != "WeeklyReport" || job.UserId is null || job.PeriodStart is null)
                    throw new InvalidOperationException("Unsupported or incomplete worker job.");

                var result = await weeklyReportService.GenerateAsync(job.UserId.Value, job.PeriodStart.Value, cancellationToken);
                if (!result.Succeeded)
                    throw new InvalidOperationException(result.ErrorMessage ?? result.ErrorCode ?? "Weekly report generation failed.");

                job.Status = WorkerJobStatus.Completed;
                job.CompletedAt = timeProvider.GetUtcNow();
                job.LastError = null;
                job.LockedBy = null;
                job.LockedAt = null;
                job.UpdatedAt = job.CompletedAt.Value;
                processed++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                job.Status = WorkerJobStatus.Pending;
                job.LockedBy = null;
                job.LockedAt = null;
                job.UpdatedAt = timeProvider.GetUtcNow();
                await repository.SaveChangesAsync(CancellationToken.None);
                throw;
            }
            catch (Exception ex)
            {
                job.AttemptCount++;
                job.LastError = ex.Message;
                job.LockedBy = null;
                job.LockedAt = null;
                job.UpdatedAt = timeProvider.GetUtcNow();

                if (job.AttemptCount >= job.MaxAttempts)
                {
                    job.Status = WorkerJobStatus.DeadLetter;
                    await repository.AddDeadLetterAsync(new WorkerDeadLetter
                    {
                        Id = Guid.NewGuid(), WorkerJobId = job.Id, JobType = job.JobType,
                        IdempotencyKey = job.IdempotencyKey, Error = ex.Message,
                        AttemptCount = job.AttemptCount, DeadLetteredAt = job.UpdatedAt,
                    }, cancellationToken);
                }
                else
                {
                    job.Status = WorkerJobStatus.Pending;
                    var seconds = Math.Min(300, 5 * (1 << Math.Min(job.AttemptCount - 1, 6)));
                    job.NextAttemptAt = job.UpdatedAt.AddSeconds(seconds);
                }
            }

            await repository.SaveChangesAsync(cancellationToken);
        }
        return processed;
    }

    public async Task RecoverStaleProcessingJobsAsync(TimeSpan staleAfter, CancellationToken cancellationToken = default)
    {
        var jobs = await repository.ListJobsAsync(100, cancellationToken);
        var threshold = timeProvider.GetUtcNow() - staleAfter;
        foreach (var job in jobs.Where(x => x.Status == WorkerJobStatus.Processing && x.LockedAt < threshold))
        {
            var tracked = await repository.GetJobByKeyAsync(job.IdempotencyKey, cancellationToken);
            if (tracked is null) continue;
            tracked.Status = WorkerJobStatus.Pending;
            tracked.LockedBy = null;
            tracked.LockedAt = null;
            tracked.NextAttemptAt = timeProvider.GetUtcNow();
            tracked.UpdatedAt = tracked.NextAttemptAt;
        }
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<ServiceResult<bool>> RecordProcessedEventAsync(Guid eventId, string eventType, CancellationToken cancellationToken = default)
    {
        if (await repository.HasProcessedEventAsync(eventId, cancellationToken)) return ServiceResult.Success(false);
        await repository.AddProcessedEventAsync(new WorkerProcessedEvent
        { EventId = eventId, EventType = eventType, ProcessedAt = timeProvider.GetUtcNow() }, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return ServiceResult.Success(true);
    }

    public async Task<ServiceResult<bool>> RecoverDeadLetterAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dead = await repository.GetDeadLetterAsync(id, cancellationToken);
        if (dead is null) return ServiceResult.Failure<bool>("dead_letter_not_found", "Dead letter was not found.");
        var job = await repository.GetJobByKeyAsync(dead.IdempotencyKey, cancellationToken);
        if (job is null) return ServiceResult.Failure<bool>("worker_job_not_found", "Worker job was not found.");
        job.Status = WorkerJobStatus.Pending; job.AttemptCount = 0; job.LastError = null;
        job.NextAttemptAt = timeProvider.GetUtcNow(); job.UpdatedAt = job.NextAttemptAt;
        dead.RecoveredAt = job.UpdatedAt;
        await repository.SaveChangesAsync(cancellationToken);
        return ServiceResult.Success(true);
    }

    public async Task<WorkerDiagnosticsSummary> GetDiagnosticsAsync(CancellationToken cancellationToken = default)
    {
        var heartbeat = await repository.GetHeartbeatAsync("weekly-report-worker", cancellationToken);
        var jobs = await repository.ListJobsAsync(50, cancellationToken);
        var dead = await repository.ListDeadLettersAsync(50, cancellationToken);
        return new WorkerDiagnosticsSummary(heartbeat?.LastSeenAt, heartbeat?.InstanceId,
            jobs.Select(ToSummary).ToList(),
            dead.Select(x => new WorkerDeadLetterSummary(x.Id, x.WorkerJobId, x.JobType, x.IdempotencyKey, x.Error, x.AttemptCount, x.DeadLetteredAt, x.RecoveredAt)).ToList());
    }

    public async Task HeartbeatAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        await repository.UpsertHeartbeatAsync(new WorkerHeartbeat
        { WorkerName = "weekly-report-worker", InstanceId = instanceId, LastSeenAt = timeProvider.GetUtcNow() }, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
    }

    private static WorkerJobSummary ToSummary(WorkerJob x) =>
        new(x.Id, x.JobType, x.IdempotencyKey, x.UserId, x.PeriodStart, x.AttemptCount,
            x.MaxAttempts, x.Status.ToString(), x.NextAttemptAt, x.UpdatedAt, x.LastError);
}
