namespace LongevityDiet.Domain.Catalog.Entities;

public sealed class RecipeIngredient
{
    public Guid RecipeId { get; set; }
    public Guid FoodId { get; set; }
    public decimal Amount { get; set; }
    public string Unit { get; set; } = string.Empty;
    public bool IsOptional { get; set; }

    public Recipe Recipe { get; set; } = null!;
    public Food Food { get; set; } = null!;
}
