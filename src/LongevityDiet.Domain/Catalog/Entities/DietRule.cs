namespace LongevityDiet.Domain.Catalog.Entities;

public sealed class DietRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RuleKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsMedicalSensitive { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<RuleVersion> Versions { get; set; } = new List<RuleVersion>();
}
