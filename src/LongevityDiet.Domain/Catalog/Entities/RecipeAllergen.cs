namespace LongevityDiet.Domain.Catalog.Entities;

public sealed class RecipeAllergen
{
    public Guid RecipeId { get; set; }
    public Guid AllergenId { get; set; }

    public Recipe Recipe { get; set; } = null!;
    public Allergen Allergen { get; set; } = null!;
}
