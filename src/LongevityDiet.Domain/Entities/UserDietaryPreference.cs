namespace LongevityDiet.Domain.Entities;

public sealed class UserDietaryPreference
{
    public Guid UserId { get; set; }
    public string DietaryPattern { get; set; } = "PlantForward";
    public string AllergiesCsv { get; set; } = string.Empty;
    public string ExcludedFoodsCsv { get; set; } = string.Empty;
    public string? PreferredCuisine { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

