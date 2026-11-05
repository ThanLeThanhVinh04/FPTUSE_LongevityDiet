using LongevityDiet.Domain.Catalog.Entities;
using LongevityDiet.Repositories.Catalog.Queries;
using Microsoft.EntityFrameworkCore;

namespace LongevityDiet.Repositories.Catalog.Repositories;

public sealed class FoodRepository(LongevityDietDbContext dbContext) : IFoodRepository
{
    public async Task<RepositoryPage<Food>> QueryAsync(
        FoodQuery query,
        CancellationToken cancellationToken = default)
    {
        var source = dbContext.Foods.AsNoTracking().AsQueryable();

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

        if (query.IsPlantBased.HasValue)
        {
            source = source.Where(x => x.IsPlantBased == query.IsPlantBased.Value);
        }

        if (query.IsLegume.HasValue)
        {
            source = source.Where(x => x.IsLegume == query.IsLegume.Value);
        }

        if (query.IsWholeGrain.HasValue)
        {
            source = source.Where(x => x.IsWholeGrain == query.IsWholeGrain.Value);
        }

        if (query.IsFish.HasValue)
        {
            source = source.Where(x => x.IsFish == query.IsFish.Value);
        }

        source = ApplyOrdering(source, query.SortField, query.SortDirection);

        var totalItems = await source.CountAsync(cancellationToken);
        var items = await source
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new RepositoryPage<Food>(items, totalItems);
    }

    public Task<Food?> FindByIdAsync(
        Guid id,
        bool includeInactive,
        bool trackChanges = false,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Food> source = dbContext.Foods;
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

    public async Task AddAsync(Food food, CancellationToken cancellationToken = default) =>
        await dbContext.Foods.AddAsync(food, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);

    private static IQueryable<Food> ApplyOrdering(
        IQueryable<Food> source,
        FoodSortField sortField,
        SortDirection direction)
    {
        return (sortField, direction) switch
        {
            (FoodSortField.Category, SortDirection.Asc) => source.OrderBy(x => x.Category).ThenBy(x => x.Id),
            (FoodSortField.Category, SortDirection.Desc) => source.OrderByDescending(x => x.Category).ThenBy(x => x.Id),
            (FoodSortField.Calories, SortDirection.Asc) => source.OrderBy(x => (double)x.Calories).ThenBy(x => x.Id),
            (FoodSortField.Calories, SortDirection.Desc) => source.OrderByDescending(x => (double)x.Calories).ThenBy(x => x.Id),
            (FoodSortField.CreatedAt, SortDirection.Asc) => source.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id),
            (FoodSortField.CreatedAt, SortDirection.Desc) => source.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id),
            (FoodSortField.Name, SortDirection.Desc) => source.OrderByDescending(x => x.NormalizedName).ThenBy(x => x.Id),
            _ => source.OrderBy(x => x.NormalizedName).ThenBy(x => x.Id),
        };
    }
}
