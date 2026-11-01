using LongevityDiet.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LongevityDiet.Repositories.Repositories;

public sealed class MealPlanRepository(LongevityDietDbContext dbContext) : IMealPlanRepository
{
    public async Task<MealPlan?> GetActiveByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var plans = await dbContext.MealPlans
            .Include(x => x.Days)
                .ThenInclude(d => d.Meals)
            .Where(x => x.UserId == userId && x.Status == MealPlanStatus.Active)
            .ToListAsync(cancellationToken);

        return plans
            .OrderByDescending(x => x.RevisionNumber)
            .ThenByDescending(x => x.GeneratedAt)
            .FirstOrDefault();
    }

    public async Task<MealPlan?> GetByIdForUserAsync(
        Guid planId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.MealPlans
            .Include(x => x.Days)
                .ThenInclude(d => d.Meals)
            .FirstOrDefaultAsync(
                x => x.Id == planId && x.UserId == userId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<MealPlan>> ListByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var plans = await dbContext.MealPlans
            .AsNoTracking()
            .Include(x => x.Days)
                .ThenInclude(d => d.Meals)
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.RevisionNumber)
            .Take(20)
            .ToListAsync(cancellationToken);

        return plans
            .OrderByDescending(x => x.GeneratedAt)
            .ThenByDescending(x => x.RevisionNumber)
            .ToList();
    }

    public Task<UserDietaryPreference?> GetDietaryPreferenceAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        dbContext.UserDietaryPreferences
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

    public async Task UpsertDietaryPreferenceAsync(
        UserDietaryPreference preference,
        CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.UserDietaryPreferences
            .FirstOrDefaultAsync(x => x.UserId == preference.UserId, cancellationToken);

        if (existing is null)
        {
            await dbContext.UserDietaryPreferences.AddAsync(preference, cancellationToken);
            return;
        }

        existing.DietaryPattern = preference.DietaryPattern;
        existing.AllergiesCsv = preference.AllergiesCsv;
        existing.ExcludedFoodsCsv = preference.ExcludedFoodsCsv;
        existing.PreferredCuisine = preference.PreferredCuisine;
        existing.UpdatedAt = preference.UpdatedAt;
    }

    public async Task AddAsync(MealPlan mealPlan, CancellationToken cancellationToken = default)
    {
        await dbContext.MealPlans.AddAsync(mealPlan, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
