namespace LongevityDiet.Domain.Catalog.Entities;

public sealed class Allergen
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<RecipeAllergen> Recipes { get; set; } = new List<RecipeAllergen>();
}
