using LongevityDiet.Domain.Entities;

namespace LongevityDiet.Repositories.Repositories;

public interface IRecommendationRepository
{
    Task<IReadOnlyList<UserFoodPreference>> ListFoodPreferencesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task UpsertFoodPreferenceAsync(UserFoodPreference preference, CancellationToken cancellationToken = default);
    Task<bool> RemoveFoodPreferenceAsync(Guid userId, string normalizedFoodToken, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecommendationFeedback>> ListFeedbackByUserAsync(Guid userId, int limit = 50, CancellationToken cancellationToken = default);
    Task AddFeedbackAsync(RecommendationFeedback feedback, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MealReplacementHistory>> ListReplacementHistoryAsync(Guid userId, Guid? mealPlanId = null, int limit = 50, CancellationToken cancellationToken = default);
    Task AddReplacementHistoryAsync(MealReplacementHistory history, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

