namespace LongevityDiet.Repositories.Catalog.Queries;

public enum SortDirection
{
    Asc = 1,
    Desc = 2
}

public enum FoodSortField
{
    Name = 1,
    Category = 2,
    Calories = 3,
    CreatedAt = 4
}

public enum RecipeSortField
{
    Name = 1,
    Category = 2,
    Cuisine = 3,
    PrepMinutes = 4,
    CreatedAt = 5
}

public sealed record FoodQuery(
    int Page,
    int PageSize,
    string? Search,
    string? Category,
    bool? IsPlantBased,
    bool? IsLegume,
    bool? IsWholeGrain,
    bool? IsFish,
    bool ActiveOnly,
    FoodSortField SortField,
    SortDirection SortDirection);

public sealed record RecipeQuery(
    int Page,
    int PageSize,
    string? Search,
    string? Category,
    string? Cuisine,
    bool? IsPlantForward,
    bool? HasLegume,
    bool? HasWholeGrain,
    bool? HasFish,
    bool ActiveOnly,
    RecipeSortField SortField,
    SortDirection SortDirection);

public sealed record RepositoryPage<T>(IReadOnlyList<T> Items, int TotalItems);
