using LongevityDiet.Domain.Catalog;

namespace LongevityDiet.Domain.Catalog.Entities;

public sealed class RuleVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DietRuleId { get; set; }
    public int VersionNumber { get; set; }
    public string ParametersJson { get; set; } = "{}";
    public string SourceUrl { get; set; } = string.Empty;
    public SourceType SourceType { get; set; }
    public string Citation { get; set; } = string.Empty;
    public EvidenceLevel EvidenceLevel { get; set; }
    public RuleVersionState State { get; set; } = RuleVersionState.Draft;
    public DateTimeOffset? EffectiveAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DietRule DietRule { get; set; } = null!;
    public ICollection<RuleSetVersion> RuleSets { get; set; } = new List<RuleSetVersion>();
}
