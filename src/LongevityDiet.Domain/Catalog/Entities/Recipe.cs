namespace LongevityDiet.Domain.Catalog.Entities;

public sealed class Recipe
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Cuisine { get; set; }
    public string? Tags { get; set; }
    public int ServingCount { get; set; } = 1;
    public int PrepMinutes { get; set; }
    public decimal CaloriesPerServing { get; set; }
    public decimal ProteinGPerServing { get; set; }
    public decimal CarbohydrateGPerServing { get; set; }
    public decimal FatGPerServing { get; set; }
    public decimal? SaturatedFatGPerServing { get; set; }
    public decimal? SugarGPerServing { get; set; }
    public decimal? FiberGPerServing { get; set; }
    public bool IsPlantForward { get; set; }
    public bool HasLegume { get; set; }
    public bool HasWholeGrain { get; set; }
    public bool HasFish { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<RecipeIngredient> Ingredients { get; set; } = new List<RecipeIngredient>();
    public ICollection<RecipeAllergen> Allergens { get; set; } = new List<RecipeAllergen>();
}
