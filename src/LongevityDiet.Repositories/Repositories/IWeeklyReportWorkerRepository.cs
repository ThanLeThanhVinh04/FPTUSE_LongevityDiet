using LongevityDiet.Domain.Entities;

namespace LongevityDiet.Repositories.Repositories;

public interface IWeeklyReportWorkerRepository
{
    Task<WeeklyReport?> GetReportAsync(Guid reportId, Guid userId, CancellationToken cancellationToken = default);
    Task<WeeklyReport?> GetReportByWeekAsync(Guid userId, DateOnly weekStart, int version, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WeeklyReport>> ListReportsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddReportAsync(WeeklyReport report, CancellationToken cancellationToken = default);
    Task<WorkerJob?> GetJobByKeyAsync(string key, CancellationToken cancellationToken = default);
    Task AddJobAsync(WorkerJob job, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkerJob>> ListRunnableJobsAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkerJob>> ListJobsAsync(int limit, CancellationToken cancellationToken = default);
    Task AddDeadLetterAsync(WorkerDeadLetter deadLetter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkerDeadLetter>> ListDeadLettersAsync(int limit, CancellationToken cancellationToken = default);
    Task<WorkerDeadLetter?> GetDeadLetterAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> HasProcessedEventAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task AddProcessedEventAsync(WorkerProcessedEvent processed, CancellationToken cancellationToken = default);
    Task UpsertHeartbeatAsync(WorkerHeartbeat heartbeat, CancellationToken cancellationToken = default);
    Task<WorkerHeartbeat?> GetHeartbeatAsync(string workerName, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
