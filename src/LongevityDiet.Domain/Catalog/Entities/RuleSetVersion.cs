namespace LongevityDiet.Domain.Catalog.Entities;

public sealed class RuleSetVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string VersionLabel { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTimeOffset? EffectiveAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<RuleVersion> RuleVersions { get; set; } = new List<RuleVersion>();
}
