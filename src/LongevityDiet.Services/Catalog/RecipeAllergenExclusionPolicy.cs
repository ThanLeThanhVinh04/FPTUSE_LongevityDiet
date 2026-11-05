namespace LongevityDiet.Services.Catalog;

public static class RecipeAllergenExclusionPolicy
{
    public static bool IsExcluded(
        IReadOnlyCollection<Guid> recipeAllergenIds,
        IReadOnlyCollection<Guid> excludedAllergenIds)
    {
        ArgumentNullException.ThrowIfNull(recipeAllergenIds);
        ArgumentNullException.ThrowIfNull(excludedAllergenIds);

        if (recipeAllergenIds.Count == 0 || excludedAllergenIds.Count == 0)
        {
            return false;
        }

        var exclusions = excludedAllergenIds.ToHashSet();
        return recipeAllergenIds.Any(exclusions.Contains);
    }
}
