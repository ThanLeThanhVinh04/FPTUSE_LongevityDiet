namespace LongevityDiet.Services.Catalog;

public static class CatalogEventNames
{
    public const string FoodCatalogChangedV1 = "FoodCatalogChanged.v1";
    public const string RecipeCatalogChangedV1 = "RecipeCatalogChanged.v1";
    public const string RuleVersionPublishedV1 = "RuleVersionPublished.v1";
    public const string RuleSetActivatedV1 = "RuleSetActivated.v1";
}

public sealed record FoodCatalogChangedV1(
    Guid FoodId,
    bool IsActive,
    DateTimeOffset ChangedAt);

public sealed record RecipeCatalogChangedV1(
    Guid RecipeId,
    bool IsActive,
    DateTimeOffset ChangedAt);

public sealed record RuleVersionPublishedV1(
    Guid RuleId,
    Guid RuleVersionId,
    int VersionNumber,
    DateTimeOffset PublishedAt);

public sealed record RuleSetActivatedV1(
    Guid RuleSetVersionId,
    string VersionLabel,
    DateTimeOffset EffectiveAt);
