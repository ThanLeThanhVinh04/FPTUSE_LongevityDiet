using LongevityDiet.Domain.Catalog;

namespace LongevityDiet.Services.Catalog;

public sealed record CatalogServiceResult<T>(
    bool Succeeded,
    T? Value,
    string? ErrorCode,
    string? ErrorMessage);

public static class CatalogServiceResult
{
    public static CatalogServiceResult<T> Success<T>(T value) =>
        new(true, value, null, null);

    public static CatalogServiceResult<T> Failure<T>(string errorCode, string errorMessage) =>
        new(false, default, errorCode, errorMessage);
}

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);

public static class PagedResultFactory
{
    public static PagedResult<T> Create<T>(
        IReadOnlyList<T> items,
        int page,
        int pageSize,
        int totalItems) =>
        new(items, page, pageSize, totalItems, (int)Math.Ceiling(totalItems / (double)pageSize));
}

public sealed record FoodQueryOptions(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string? Category = null,
    bool? IsPlantBased = null,
    bool? IsLegume = null,
    bool? IsWholeGrain = null,
    bool? IsFish = null,
    string SortBy = "name",
    string SortDirection = "asc");

public sealed record RecipeQueryOptions(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string? Category = null,
    string? Cuisine = null,
    bool? IsPlantForward = null,
    bool? HasLegume = null,
    bool? HasWholeGrain = null,
    bool? HasFish = null,
    string SortBy = "name",
    string SortDirection = "asc");

public sealed record FoodDto(
    Guid Id,
    string Name,
    string Category,
    string? Tags,
    string? Cuisine,
    decimal DefaultServingAmount,
    string ServingUnit,
    decimal Calories,
    decimal ProteinG,
    decimal CarbohydrateG,
    decimal FatG,
    decimal? SaturatedFatG,
    decimal? SugarG,
    decimal? FiberG,
    bool IsPlantBased,
    bool IsLegume,
    bool IsWholeGrain,
    bool IsFish,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record SaveFoodCommand(
    string Name,
    string Category,
    string? Tags,
    string? Cuisine,
    decimal DefaultServingAmount,
    string ServingUnit,
    decimal Calories,
    decimal ProteinG,
    decimal CarbohydrateG,
    decimal FatG,
    decimal? SaturatedFatG,
    decimal? SugarG,
    decimal? FiberG,
    bool IsPlantBased,
    bool IsLegume,
    bool IsWholeGrain,
    bool IsFish);

public sealed record RecipeIngredientInput(
    Guid FoodId,
    decimal Amount,
    string Unit,
    bool IsOptional);

public sealed record SaveRecipeCommand(
    string Name,
    string Description,
    string Category,
    string? Cuisine,
    string? Tags,
    int ServingCount,
    int PrepMinutes,
    decimal CaloriesPerServing,
    decimal ProteinGPerServing,
    decimal CarbohydrateGPerServing,
    decimal FatGPerServing,
    decimal? SaturatedFatGPerServing,
    decimal? SugarGPerServing,
    decimal? FiberGPerServing,
    bool IsPlantForward,
    bool HasLegume,
    bool HasWholeGrain,
    bool HasFish,
    IReadOnlyList<RecipeIngredientInput> Ingredients,
    IReadOnlyList<Guid> AllergenIds);

public sealed record RecipeSummaryDto(
    Guid Id,
    string Name,
    string Category,
    string? Cuisine,
    string? Tags,
    int ServingCount,
    int PrepMinutes,
    decimal CaloriesPerServing,
    bool IsPlantForward,
    bool HasLegume,
    bool HasWholeGrain,
    bool HasFish,
    bool IsActive);

public sealed record RecipeIngredientDto(
    Guid FoodId,
    string FoodName,
    decimal Amount,
    string Unit,
    bool IsOptional);

public sealed record AllergenDto(Guid Id, string Code, string Name);

public sealed record RecipeDetailDto(
    Guid Id,
    string Name,
    string Description,
    string Category,
    string? Cuisine,
    string? Tags,
    int ServingCount,
    int PrepMinutes,
    decimal CaloriesPerServing,
    decimal ProteinGPerServing,
    decimal CarbohydrateGPerServing,
    decimal FatGPerServing,
    decimal? SaturatedFatGPerServing,
    decimal? SugarGPerServing,
    decimal? FiberGPerServing,
    bool IsPlantForward,
    bool HasLegume,
    bool HasWholeGrain,
    bool HasFish,
    bool IsActive,
    IReadOnlyList<RecipeIngredientDto> Ingredients,
    IReadOnlyList<AllergenDto> Allergens,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateDietRuleCommand(
    string RuleKey,
    string Title,
    string Description,
    string Category,
    bool IsMedicalSensitive);

public sealed record CreateRuleVersionCommand(
    string ParametersJson,
    string SourceUrl,
    SourceType SourceType,
    string Citation,
    EvidenceLevel EvidenceLevel,
    DateTimeOffset? EffectiveAt);

public sealed record UpdateRuleVersionCommand(
    string ParametersJson,
    string SourceUrl,
    SourceType SourceType,
    string Citation,
    EvidenceLevel EvidenceLevel,
    DateTimeOffset? EffectiveAt);

public sealed record DietRuleDto(
    Guid Id,
    string RuleKey,
    string Title,
    string Description,
    string Category,
    bool IsMedicalSensitive,
    bool IsActive,
    IReadOnlyList<RuleVersionDto> Versions);

public sealed record RuleVersionDto(
    Guid Id,
    Guid DietRuleId,
    int VersionNumber,
    string ParametersJson,
    string SourceUrl,
    SourceType SourceType,
    string Citation,
    EvidenceLevel EvidenceLevel,
    RuleVersionState State,
    DateTimeOffset? EffectiveAt,
    DateTimeOffset? PublishedAt);

public sealed record RuleSetDto(
    Guid Id,
    string VersionLabel,
    bool IsActive,
    DateTimeOffset? EffectiveAt,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<RuleVersionDto> Rules);
