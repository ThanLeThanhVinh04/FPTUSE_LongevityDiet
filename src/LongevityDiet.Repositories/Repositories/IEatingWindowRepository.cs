using LongevityDiet.Domain.Entities;

namespace LongevityDiet.Repositories.Repositories;

public interface IEatingWindowRepository
{
    Task<EatingWindowSnapshot?> GetByUserAndDateAsync(
        Guid userId,
        DateOnly localDate,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EatingWindowSnapshot>> ListByDateRangeAsync(
        Guid userId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default);
    Task UpsertSnapshotAsync(
        EatingWindowSnapshot snapshot,
        CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

