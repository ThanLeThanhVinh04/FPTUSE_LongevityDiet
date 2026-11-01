using LongevityDiet.Domain.Entities;

namespace LongevityDiet.Repositories.Repositories;

public interface IMealLogRepository
{
    Task<MealLog?> GetByIdForUserAsync(Guid logId, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MealLog>> ListByDateRangeAsync(
        Guid userId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MealLog>> ListActiveByLocalDateAsync(
        Guid userId,
        DateOnly localDate,
        CancellationToken cancellationToken = default);
    Task AddAsync(MealLog mealLog, CancellationToken cancellationToken = default);
    void ReplaceItems(MealLog mealLog, IReadOnlyList<MealLogItem> newItems);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

