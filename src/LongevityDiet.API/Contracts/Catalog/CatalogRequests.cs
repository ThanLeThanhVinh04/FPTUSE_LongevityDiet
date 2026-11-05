using LongevityDiet.Domain.Catalog;

namespace LongevityDiet.API.Contracts.Catalog;

public sealed record SaveFoodRequest(
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

public sealed record RecipeIngredientRequest(
    Guid FoodId,
    decimal Amount,
    string Unit,
    bool IsOptional);

public sealed record SaveRecipeRequest(
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
    IReadOnlyList<RecipeIngredientRequest> Ingredients,
    IReadOnlyList<Guid> AllergenIds);

public sealed record CreateDietRuleRequest(
    string RuleKey,
    string Title,
    string Description,
    string Category,
    bool IsMedicalSensitive);

public sealed record SaveRuleVersionRequest(
    string ParametersJson,
    string SourceUrl,
    SourceType SourceType,
    string Citation,
    EvidenceLevel EvidenceLevel,
    DateTimeOffset? EffectiveAt);
