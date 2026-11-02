using LongevityDiet.Domain.Entities;

namespace LongevityDiet.Repositories.Repositories;

public interface IFmdRepository
{
    Task<FmdEducationAcknowledgement?> GetLatestAcknowledgementAsync(Guid userId, string? version = null, CancellationToken cancellationToken = default);
    Task AddAcknowledgementAsync(FmdEducationAcknowledgement acknowledgement, CancellationToken cancellationToken = default);
    Task<FmdSafetyAssessment?> GetLatestAssessmentAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAssessmentAsync(FmdSafetyAssessment assessment, CancellationToken cancellationToken = default);
    Task<FmdCycle?> GetActiveCycleAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<FmdCycle?> GetCycleByIdAsync(Guid cycleId, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FmdCycle>> ListCyclesByUserAsync(Guid userId, int limit = 20, CancellationToken cancellationToken = default);
    Task AddCycleAsync(FmdCycle cycle, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

