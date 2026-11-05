using LongevityDiet.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LongevityDiet.Repositories.Repositories;

public sealed class RecommendationRepository(LongevityDietDbContext dbContext) : IRecommendationRepository
{
    public async Task<IReadOnlyList<UserFoodPreference>> ListFoodPreferencesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var items = await dbContext.UserFoodPreferences
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);

        return items
            .OrderBy(x => x.PreferenceKind)
            .ThenBy(x => x.FoodToken, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task UpsertFoodPreferenceAsync(
        UserFoodPreference preference,
        CancellationToken cancellationToken = default)
    {
        var normalizedToken = preference.FoodToken.Trim().ToLowerInvariant();
        var existing = await dbContext.UserFoodPreferences
            .FirstOrDefaultAsync(
                x => x.UserId == preference.UserId && x.FoodToken == normalizedToken,
                cancellationToken);

        if (existing is null)
        {
            preference.FoodToken = normalizedToken;
            await dbContext.UserFoodPreferences.AddAsync(preference, cancellationToken);
            return;
        }

        existing.DisplayLabel = preference.DisplayLabel;
        existing.PreferenceKind = preference.PreferenceKind;
        existing.Source = preference.Source;
        existing.Notes = preference.Notes;
        existing.UpdatedAt = preference.UpdatedAt;
    }

    public async Task<bool> RemoveFoodPreferenceAsync(
        Guid userId,
        string normalizedFoodToken,
        CancellationToken cancellationToken = default)
    {
        var token = normalizedFoodToken.Trim().ToLowerInvariant();
        var existing = await dbContext.UserFoodPreferences
            .FirstOrDefaultAsync(
                x => x.UserId == userId && x.FoodToken == token,
                cancellationToken);

        if (existing is null)
        {
            return false;
        }

        dbContext.UserFoodPreferences.Remove(existing);
        return true;
    }

    public async Task<IReadOnlyList<RecommendationFeedback>> ListFeedbackByUserAsync(
        Guid userId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var items = await dbContext.RecommendationFeedbacks
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);

        return items
            .OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.CandidateCode, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(limit, 1, 200))
            .ToList();
    }

    public async Task AddFeedbackAsync(
        RecommendationFeedback feedback,
        CancellationToken cancellationToken = default)
    {
        await dbContext.RecommendationFeedbacks.AddAsync(feedback, cancellationToken);
    }

    public async Task<IReadOnlyList<MealReplacementHistory>> ListReplacementHistoryAsync(
        Guid userId,
        Guid? mealPlanId = null,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.MealReplacementHistories
            .AsNoTracking()
            .Where(x => x.UserId == userId);

        if (mealPlanId.HasValue)
        {
            query = query.Where(x => x.MealPlanId == mealPlanId.Value);
        }

        var items = await query.ToListAsync(cancellationToken);
        return items
            .OrderByDescending(x => x.ReplacedAt)
            .ThenBy(x => x.DayNumber)
            .Take(Math.Clamp(limit, 1, 200))
            .ToList();
    }

    public async Task AddReplacementHistoryAsync(
        MealReplacementHistory history,
        CancellationToken cancellationToken = default)
    {
        await dbContext.MealReplacementHistories.AddAsync(history, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

