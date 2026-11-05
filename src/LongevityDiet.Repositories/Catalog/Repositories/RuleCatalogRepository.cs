using LongevityDiet.Domain.Catalog;
using LongevityDiet.Domain.Catalog.Entities;
using Microsoft.EntityFrameworkCore;

namespace LongevityDiet.Repositories.Catalog.Repositories;

public sealed class RuleCatalogRepository(LongevityDietDbContext dbContext) : IRuleCatalogRepository
{
    public async Task<IReadOnlyList<DietRule>> GetActiveRulesAsync(
        CancellationToken cancellationToken = default) =>
        await dbContext.DietRules
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Include(x => x.Versions.Where(v => v.State == RuleVersionState.Published))
            .OrderBy(x => x.RuleKey)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DietRule>> GetAllRulesAsync(
        CancellationToken cancellationToken = default) =>
        await dbContext.DietRules
            .AsNoTracking()
            .Include(x => x.Versions)
            .OrderBy(x => x.RuleKey)
            .ToListAsync(cancellationToken);

    public Task<DietRule?> FindRuleAsync(
        Guid id,
        bool includeInactive,
        bool trackChanges = false,
        CancellationToken cancellationToken = default)
    {
        IQueryable<DietRule> source = dbContext.DietRules
            .Include(x => x.Versions);

        if (!trackChanges)
        {
            source = source.AsNoTracking();
        }

        if (!includeInactive)
        {
            source = source.Where(x => x.IsActive);
        }

        return source.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<RuleVersion?> FindVersionAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        dbContext.RuleVersions
            .Include(x => x.DietRule)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<RuleSetVersion?> GetActiveRuleSetAsync(
        CancellationToken cancellationToken = default) =>
        dbContext.RuleSetVersions
            .AsNoTracking()
            .Include(x => x.RuleVersions)
            .ThenInclude(x => x.DietRule)
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.EffectiveAt)
            .ThenBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<RuleVersion>> FindVersionsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default) =>
        await dbContext.RuleVersions
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);

    public async Task AddRuleAsync(DietRule rule, CancellationToken cancellationToken = default) =>
        await dbContext.DietRules.AddAsync(rule, cancellationToken);

    public async Task AddVersionAsync(RuleVersion version, CancellationToken cancellationToken = default) =>
        await dbContext.RuleVersions.AddAsync(version, cancellationToken);

    public async Task AddRuleSetAsync(RuleSetVersion ruleSet, CancellationToken cancellationToken = default) =>
        await dbContext.RuleSetVersions.AddAsync(ruleSet, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
