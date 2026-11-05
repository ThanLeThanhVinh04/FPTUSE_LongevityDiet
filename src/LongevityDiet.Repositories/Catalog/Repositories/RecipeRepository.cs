using LongevityDiet.Domain.Catalog.Entities;
using LongevityDiet.Repositories.Catalog.Queries;
using Microsoft.EntityFrameworkCore;

namespace LongevityDiet.Repositories.Catalog.Repositories;

public sealed class RecipeRepository(LongevityDietDbContext dbContext) : IRecipeRepository
{
    public async Task<RepositoryPage<Recipe>> QueryAsync(
        RecipeQuery query,
        CancellationToken cancellationToken = default)
    {
        var source = dbContext.Recipes.AsNoTracking().AsQueryable();

        if (query.ActiveOnly)
        {
            source = source.Where(x => x.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var normalized = query.Search.Trim().ToUpperInvariant();
            source = source.Where(x =>
                x.NormalizedName.Contains(normalized) ||
                x.Name.Contains(query.Search.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            source = source.Where(x => x.Category == query.Category);
        }

        if (!string.IsNullOrWhiteSpace(query.Cuisine))
        {
            source = source.Where(x => x.Cuisine == query.Cuisine);
        }

        if (query.IsPlantForward.HasValue)
        {
            source = source.Where(x => x.IsPlantForward == query.IsPlantForward.Value);
        }

        if (query.HasLegume.HasValue)
        {
            source = source.Where(x => x.HasLegume == query.HasLegume.Value);
        }

        if (query.HasWholeGrain.HasValue)
        {
            source = source.Where(x => x.HasWholeGrain == query.HasWholeGrain.Value);
        }

        if (query.HasFish.HasValue)
        {
            source = source.Where(x => x.HasFish == query.HasFish.Value);
        }

        source = ApplyOrdering(source, query.SortField, query.SortDirection);

        var totalItems = await source.CountAsync(cancellationToken);
        var items = await source
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new RepositoryPage<Recipe>(items, totalItems);
    }

    public Task<Recipe?> FindByIdAsync(
        Guid id,
        bool includeInactive,
        bool trackChanges = false,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Recipe> source = dbContext.Recipes
            .Include(x => x.Ingredients)
            .ThenInclude(x => x.Food)
            .Include(x => x.Allergens)
            .ThenInclude(x => x.Allergen);

        if (!trackChanges)
        {
            source = source.AsNoTracking();
        }

        if (!includeInactive)
        {
            source = source.Where(x => x.IsActive);
        }

        return source.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Food>> FindFoodsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default) =>
        await dbContext.Foods
            .AsNoTracking()
            .Where(x => ids.Contains(x.Id) && x.IsActive)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Allergen>> GetActiveAllergensAsync(
        CancellationToken cancellationToken = default) =>
        await dbContext.Allergens
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Code)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Allergen>> FindAllergensAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default) =>
        await dbContext.Allergens
            .AsNoTracking()
            .Where(x => ids.Contains(x.Id) && x.IsActive)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Recipe recipe, CancellationToken cancellationToken = default) =>
        await dbContext.Recipes.AddAsync(recipe, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);

    private static IQueryable<Recipe> ApplyOrdering(
        IQueryable<Recipe> source,
        RecipeSortField sortField,
        SortDirection direction)
    {
        return (sortField, direction) switch
        {
            (RecipeSortField.Category, SortDirection.Asc) => source.OrderBy(x => x.Category).ThenBy(x => x.Id),
            (RecipeSortField.Category, SortDirection.Desc) => source.OrderByDescending(x => x.Category).ThenBy(x => x.Id),
            (RecipeSortField.Cuisine, SortDirection.Asc) => source.OrderBy(x => x.Cuisine).ThenBy(x => x.Id),
            (RecipeSortField.Cuisine, SortDirection.Desc) => source.OrderByDescending(x => x.Cuisine).ThenBy(x => x.Id),
            (RecipeSortField.PrepMinutes, SortDirection.Asc) => source.OrderBy(x => x.PrepMinutes).ThenBy(x => x.Id),
            (RecipeSortField.PrepMinutes, SortDirection.Desc) => source.OrderByDescending(x => x.PrepMinutes).ThenBy(x => x.Id),
            (RecipeSortField.CreatedAt, SortDirection.Asc) => source.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id),
            (RecipeSortField.CreatedAt, SortDirection.Desc) => source.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id),
            (RecipeSortField.Name, SortDirection.Desc) => source.OrderByDescending(x => x.NormalizedName).ThenBy(x => x.Id),
            _ => source.OrderBy(x => x.NormalizedName).ThenBy(x => x.Id),
        };
    }
}
