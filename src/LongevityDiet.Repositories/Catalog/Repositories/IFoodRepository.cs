using LongevityDiet.Domain.Catalog.Entities;
using LongevityDiet.Repositories.Catalog.Queries;

namespace LongevityDiet.Repositories.Catalog.Repositories;

public interface IFoodRepository
{
    Task<RepositoryPage<Food>> QueryAsync(FoodQuery query, CancellationToken cancellationToken = default);
    Task<Food?> FindByIdAsync(Guid id, bool includeInactive, bool trackChanges = false, CancellationToken cancellationToken = default);
    Task AddAsync(Food food, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
