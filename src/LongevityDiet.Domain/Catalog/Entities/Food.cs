namespace LongevityDiet.Domain.Catalog.Entities;

public sealed class Food
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Tags { get; set; }
    public string? Cuisine { get; set; }
    public decimal DefaultServingAmount { get; set; }
    public string ServingUnit { get; set; } = string.Empty;
    public decimal Calories { get; set; }
    public decimal ProteinG { get; set; }
    public decimal CarbohydrateG { get; set; }
    public decimal FatG { get; set; }
    public decimal? SaturatedFatG { get; set; }
    public decimal? SugarG { get; set; }
    public decimal? FiberG { get; set; }
    public bool IsPlantBased { get; set; }
    public bool IsLegume { get; set; }
    public bool IsWholeGrain { get; set; }
    public bool IsFish { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = new List<RecipeIngredient>();
}
