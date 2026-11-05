using LongevityDiet.Domain.Catalog.Entities;
using LongevityDiet.Repositories.Catalog.Queries;

namespace LongevityDiet.Repositories.Catalog.Repositories;

public interface IRecipeRepository
{
    Task<RepositoryPage<Recipe>> QueryAsync(RecipeQuery query, CancellationToken cancellationToken = default);
    Task<Recipe?> FindByIdAsync(Guid id, bool includeInactive, bool trackChanges = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Food>> FindFoodsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Allergen>> GetActiveAllergensAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Allergen>> FindAllergensAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
    Task AddAsync(Recipe recipe, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
