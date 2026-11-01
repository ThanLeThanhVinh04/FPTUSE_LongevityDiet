using LongevityDiet.Domain.Entities;

namespace LongevityDiet.Repositories.Repositories;

public interface IMealPlanRepository
{
    Task<MealPlan?> GetActiveByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<MealPlan?> GetByIdForUserAsync(Guid planId, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MealPlan>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserDietaryPreference?> GetDietaryPreferenceAsync(Guid userId, CancellationToken cancellationToken = default);
    Task UpsertDietaryPreferenceAsync(UserDietaryPreference preference, CancellationToken cancellationToken = default);
    Task AddAsync(MealPlan mealPlan, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

