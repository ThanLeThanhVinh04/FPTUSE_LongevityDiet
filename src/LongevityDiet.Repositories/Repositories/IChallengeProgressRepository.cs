using LongevityDiet.Domain.Entities;

namespace LongevityDiet.Repositories.Repositories;

public interface IChallengeProgressRepository
{
    Task<IReadOnlyList<ChallengeTemplate>> ListActiveTemplatesAsync(CancellationToken cancellationToken = default);
    Task<ChallengeTemplate?> GetTemplateAsync(Guid templateId, CancellationToken cancellationToken = default);
    Task<UserChallenge?> GetActiveChallengeAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserChallenge?> GetChallengeAsync(Guid challengeId, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserChallenge>> ListHistoryAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddChallengeAsync(UserChallenge challenge, CancellationToken cancellationToken = default);
    Task<DailyWellnessSummary?> GetDailySummaryAsync(Guid userId, DateOnly localDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DailyWellnessSummary>> ListDailySummariesAsync(Guid userId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default);
    Task UpsertDailySummaryAsync(DailyWellnessSummary summary, CancellationToken cancellationToken = default);
    Task<bool> HasProcessedEventAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task AddProcessedEventAsync(ProgressEventReceipt receipt, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
