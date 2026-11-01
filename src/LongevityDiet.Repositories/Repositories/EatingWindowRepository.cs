using LongevityDiet.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LongevityDiet.Repositories.Repositories;

public sealed class EatingWindowRepository(LongevityDietDbContext dbContext) : IEatingWindowRepository
{
    public Task<EatingWindowSnapshot?> GetByUserAndDateAsync(
        Guid userId,
        DateOnly localDate,
        CancellationToken cancellationToken = default) =>
        dbContext.EatingWindowSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.UserId == userId && x.LocalDate == localDate,
                cancellationToken);

    public async Task<IReadOnlyList<EatingWindowSnapshot>> ListByDateRangeAsync(
        Guid userId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.EatingWindowSnapshots
            .AsNoTracking()
            .Where(x =>
                x.UserId == userId &&
                x.LocalDate >= fromDate &&
                x.LocalDate <= toDate)
            .OrderBy(x => x.LocalDate)
            .ToListAsync(cancellationToken);
    }

    public async Task UpsertSnapshotAsync(
        EatingWindowSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.EatingWindowSnapshots
            .FirstOrDefaultAsync(
                x => x.UserId == snapshot.UserId && x.LocalDate == snapshot.LocalDate,
                cancellationToken);

        if (existing is null)
        {
            await dbContext.EatingWindowSnapshots.AddAsync(snapshot, cancellationToken);
            return;
        }

        existing.TimeZone = snapshot.TimeZone;
        existing.LoggedMealCount = snapshot.LoggedMealCount;
        existing.CaloricMealCount = snapshot.CaloricMealCount;
        existing.TotalCaloriesKcal = snapshot.TotalCaloriesKcal;
        existing.FirstMealAt = snapshot.FirstMealAt;
        existing.LastMealAt = snapshot.LastMealAt;
        existing.FirstMealLocalTime = snapshot.FirstMealLocalTime;
        existing.LastMealLocalTime = snapshot.LastMealLocalTime;
        existing.EatingWindowMinutes = snapshot.EatingWindowMinutes;
        existing.TargetWindowMinutes = snapshot.TargetWindowMinutes;
        existing.IsWithinTargetWindow = snapshot.IsWithinTargetWindow;
        existing.ConfiguredSleepTime = snapshot.ConfiguredSleepTime;
        existing.MinutesBeforeBedtime = snapshot.MinutesBeforeBedtime;
        existing.IsLateEatingFlagged = snapshot.IsLateEatingFlagged;
        existing.StatusCode = snapshot.StatusCode;
        existing.SummaryMessage = snapshot.SummaryMessage;
        existing.CalculatedAt = snapshot.CalculatedAt;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

