using LongevityDiet.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LongevityDiet.Repositories.Repositories;

public sealed class MealLogRepository(LongevityDietDbContext dbContext) : IMealLogRepository
{
    public Task<MealLog?> GetByIdForUserAsync(
        Guid logId,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        dbContext.MealLogs
            .Include(x => x.Items)
            .FirstOrDefaultAsync(
                x => x.Id == logId && x.UserId == userId && !x.IsDeleted,
                cancellationToken);

    public async Task<IReadOnlyList<MealLog>> ListByDateRangeAsync(
        Guid userId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        var logs = await dbContext.MealLogs
            .AsNoTracking()
            .Include(x => x.Items)
            .Where(x =>
                x.UserId == userId &&
                !x.IsDeleted &&
                x.LocalDate >= fromDate &&
                x.LocalDate <= toDate)
            .ToListAsync(cancellationToken);

        return logs
            .OrderBy(x => x.LocalDate)
            .ThenBy(x => x.EatenAt)
            .ThenBy(x => x.Id)
            .ToList();
    }

    public async Task<IReadOnlyList<MealLog>> ListActiveByLocalDateAsync(
        Guid userId,
        DateOnly localDate,
        CancellationToken cancellationToken = default)
    {
        var logs = await dbContext.MealLogs
            .AsNoTracking()
            .Include(x => x.Items)
            .Where(x =>
                x.UserId == userId &&
                !x.IsDeleted &&
                x.LocalDate == localDate)
            .ToListAsync(cancellationToken);

        return logs
            .OrderBy(x => x.EatenAt)
            .ThenBy(x => x.Id)
            .ToList();
    }

    public async Task AddAsync(MealLog mealLog, CancellationToken cancellationToken = default)
    {
        await dbContext.MealLogs.AddAsync(mealLog, cancellationToken);
    }

    public void ReplaceItems(MealLog mealLog, IReadOnlyList<MealLogItem> newItems)
    {
        dbContext.MealLogItems.RemoveRange(mealLog.Items);
        mealLog.Items.Clear();

        foreach (var item in newItems)
        {
            mealLog.Items.Add(item);
        }

        dbContext.MealLogItems.AddRange(newItems);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

