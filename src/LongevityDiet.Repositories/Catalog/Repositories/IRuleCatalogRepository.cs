using LongevityDiet.Domain.Catalog.Entities;

namespace LongevityDiet.Repositories.Catalog.Repositories;

public interface IRuleCatalogRepository
{
    Task<IReadOnlyList<DietRule>> GetActiveRulesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DietRule>> GetAllRulesAsync(CancellationToken cancellationToken = default);
    Task<DietRule?> FindRuleAsync(Guid id, bool includeInactive, bool trackChanges = false, CancellationToken cancellationToken = default);
    Task<RuleVersion?> FindVersionAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RuleSetVersion?> GetActiveRuleSetAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RuleVersion>> FindVersionsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
    Task AddRuleAsync(DietRule rule, CancellationToken cancellationToken = default);
    Task AddVersionAsync(RuleVersion version, CancellationToken cancellationToken = default);
    Task AddRuleSetAsync(RuleSetVersion ruleSet, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
