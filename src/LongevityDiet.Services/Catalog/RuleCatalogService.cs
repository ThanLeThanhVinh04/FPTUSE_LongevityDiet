using LongevityDiet.Domain.Catalog;
using LongevityDiet.Domain.Catalog.Entities;
using LongevityDiet.Repositories.Catalog.Repositories;

namespace LongevityDiet.Services.Catalog;

public sealed class RuleCatalogService(
    IRuleCatalogRepository rules,
    TimeProvider timeProvider)
{
    public async Task<CatalogServiceResult<IReadOnlyList<DietRuleDto>>> GetActiveRulesAsync(
        CancellationToken cancellationToken = default)
    {
        var items = await rules.GetActiveRulesAsync(cancellationToken);
        return CatalogServiceResult.Success<IReadOnlyList<DietRuleDto>>(
            items.Select(ToRuleDto).ToList());
    }

    public async Task<CatalogServiceResult<IReadOnlyList<DietRuleDto>>> GetAllRulesAsync(
        CancellationToken cancellationToken = default)
    {
        var items = await rules.GetAllRulesAsync(cancellationToken);
        return CatalogServiceResult.Success<IReadOnlyList<DietRuleDto>>(
            items.Select(ToRuleDto).ToList());
    }

    public async Task<CatalogServiceResult<DietRuleDto>> GetAsync(
        Guid id,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var rule = await rules.FindRuleAsync(id, includeInactive, cancellationToken: cancellationToken);
        if (rule is null)
        {
            return CatalogServiceResult.Failure<DietRuleDto>(
                "rule_not_found",
                "Diet rule was not found.");
        }

        if (!includeInactive)
        {
            rule.Versions = rule.Versions
                .Where(x => x.State == RuleVersionState.Published)
                .OrderBy(x => x.VersionNumber)
                .ToList();
        }

        return CatalogServiceResult.Success(ToRuleDto(rule));
    }

    public async Task<CatalogServiceResult<DietRuleDto>> CreateRuleAsync(
        CreateDietRuleCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.RuleKey) ||
            string.IsNullOrWhiteSpace(command.Title) ||
            string.IsNullOrWhiteSpace(command.Description) ||
            string.IsNullOrWhiteSpace(command.Category))
        {
            return CatalogServiceResult.Failure<DietRuleDto>(
                "invalid_rule",
                "Rule key, title, description and category are required.");
        }

        var now = timeProvider.GetUtcNow();
        var rule = new DietRule
        {
            RuleKey = command.RuleKey.Trim().ToUpperInvariant(),
            Title = command.Title.Trim(),
            Description = command.Description.Trim(),
            Category = command.Category.Trim(),
            IsMedicalSensitive = command.IsMedicalSensitive,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await rules.AddRuleAsync(rule, cancellationToken);
        await rules.SaveChangesAsync(cancellationToken);
        return CatalogServiceResult.Success(ToRuleDto(rule));
    }

    public async Task<CatalogServiceResult<DietRuleDto>> UpdateRuleAsync(
        Guid ruleId,
        CreateDietRuleCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.RuleKey) ||
            string.IsNullOrWhiteSpace(command.Title) ||
            string.IsNullOrWhiteSpace(command.Description) ||
            string.IsNullOrWhiteSpace(command.Category))
        {
            return CatalogServiceResult.Failure<DietRuleDto>(
                "invalid_rule",
                "Rule key, title, description and category are required.");
        }

        var rule = await rules.FindRuleAsync(ruleId, includeInactive: true, trackChanges: true, cancellationToken: cancellationToken);
        if (rule is null)
        {
            return CatalogServiceResult.Failure<DietRuleDto>(
                "rule_not_found",
                "Diet rule was not found.");
        }

        rule.RuleKey = command.RuleKey.Trim().ToUpperInvariant();
        rule.Title = command.Title.Trim();
        rule.Description = command.Description.Trim();
        rule.Category = command.Category.Trim();
        rule.IsMedicalSensitive = command.IsMedicalSensitive;
        rule.UpdatedAt = timeProvider.GetUtcNow();

        await rules.SaveChangesAsync(cancellationToken);
        return CatalogServiceResult.Success(ToRuleDto(rule));
    }

    public async Task<CatalogServiceResult<RuleVersionDto>> CreateVersionAsync(
        Guid ruleId,
        CreateRuleVersionCommand command,
        CancellationToken cancellationToken = default)
    {
        var rule = await rules.FindRuleAsync(
            ruleId,
            includeInactive: true,
            cancellationToken: cancellationToken);

        if (rule is null)
        {
            return CatalogServiceResult.Failure<RuleVersionDto>(
                "rule_not_found",
                "Diet rule was not found.");
        }

        var validation = ValidateVersion(command.ParametersJson, command.SourceUrl, command.Citation);
        if (validation is not null)
        {
            return CatalogServiceResult.Failure<RuleVersionDto>(
                validation.Value.Code,
                validation.Value.Message);
        }

        var version = new RuleVersion
        {
            DietRuleId = rule.Id,
            VersionNumber = rule.Versions.Count == 0
                ? 1
                : rule.Versions.Max(x => x.VersionNumber) + 1,
            ParametersJson = command.ParametersJson.Trim(),
            SourceUrl = command.SourceUrl.Trim(),
            SourceType = command.SourceType,
            Citation = command.Citation.Trim(),
            EvidenceLevel = command.EvidenceLevel,
            State = RuleVersionState.Draft,
            EffectiveAt = command.EffectiveAt,
            CreatedAt = timeProvider.GetUtcNow(),
        };

        await rules.AddVersionAsync(version, cancellationToken);
        await rules.SaveChangesAsync(cancellationToken);
        return CatalogServiceResult.Success(ToVersionDto(version));
    }

    public async Task<CatalogServiceResult<RuleVersionDto>> UpdateVersionAsync(
        Guid versionId,
        UpdateRuleVersionCommand command,
        CancellationToken cancellationToken = default)
    {
        var version = await rules.FindVersionAsync(versionId, cancellationToken);
        if (version is null)
        {
            return CatalogServiceResult.Failure<RuleVersionDto>(
                "rule_version_not_found",
                "Rule version was not found.");
        }

        if (version.State == RuleVersionState.Published)
        {
            return CatalogServiceResult.Failure<RuleVersionDto>(
                "published_rule_immutable",
                "Published rule versions cannot be modified.");
        }

        var validation = ValidateVersion(command.ParametersJson, command.SourceUrl, command.Citation);
        if (validation is not null)
        {
            return CatalogServiceResult.Failure<RuleVersionDto>(
                validation.Value.Code,
                validation.Value.Message);
        }

        version.ParametersJson = command.ParametersJson.Trim();
        version.SourceUrl = command.SourceUrl.Trim();
        version.SourceType = command.SourceType;
        version.Citation = command.Citation.Trim();
        version.EvidenceLevel = command.EvidenceLevel;
        version.EffectiveAt = command.EffectiveAt;

        await rules.SaveChangesAsync(cancellationToken);
        return CatalogServiceResult.Success(ToVersionDto(version));
    }

    public async Task<CatalogServiceResult<RuleVersionDto>> PublishVersionAsync(
        Guid versionId,
        CancellationToken cancellationToken = default)
    {
        var version = await rules.FindVersionAsync(versionId, cancellationToken);
        if (version is null)
        {
            return CatalogServiceResult.Failure<RuleVersionDto>(
                "rule_version_not_found",
                "Rule version was not found.");
        }

        if (version.State == RuleVersionState.Published)
        {
            return CatalogServiceResult.Failure<RuleVersionDto>(
                "rule_version_conflict",
                "Rule version is already published.");
        }

        if (string.IsNullOrWhiteSpace(version.SourceUrl) ||
            string.IsNullOrWhiteSpace(version.Citation))
        {
            return CatalogServiceResult.Failure<RuleVersionDto>(
                "missing_provenance",
                "A source URL and citation are required before publication.");
        }

        version.State = RuleVersionState.Published;
        version.PublishedAt = timeProvider.GetUtcNow();
        version.EffectiveAt ??= version.PublishedAt;

        await rules.SaveChangesAsync(cancellationToken);
        return CatalogServiceResult.Success(ToVersionDto(version));
    }

    public async Task<CatalogServiceResult<RuleSetDto>> GetActiveRuleSetAsync(
        CancellationToken cancellationToken = default)
    {
        var ruleSet = await rules.GetActiveRuleSetAsync(cancellationToken);
        return ruleSet is null
            ? CatalogServiceResult.Failure<RuleSetDto>(
                "active_rule_set_not_found",
                "No active rule set is available.")
            : CatalogServiceResult.Success(ToRuleSetDto(ruleSet));
    }

    private static (string Code, string Message)? ValidateVersion(
        string parametersJson,
        string sourceUrl,
        string citation)
    {
        if (string.IsNullOrWhiteSpace(parametersJson))
        {
            return ("invalid_parameters", "Rule parameters are required.");
        }

        if (string.IsNullOrWhiteSpace(sourceUrl) || string.IsNullOrWhiteSpace(citation))
        {
            return ("missing_provenance", "A source URL and citation are required.");
        }

        if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return ("invalid_source_url", "Source URL must be an absolute HTTP or HTTPS URL.");
        }

        return null;
    }

    private static DietRuleDto ToRuleDto(DietRule rule) =>
        new(
            rule.Id,
            rule.RuleKey,
            rule.Title,
            rule.Description,
            rule.Category,
            rule.IsMedicalSensitive,
            rule.IsActive,
            rule.Versions
                .OrderBy(x => x.VersionNumber)
                .Select(ToVersionDto)
                .ToList());

    private static RuleVersionDto ToVersionDto(RuleVersion version) =>
        new(
            version.Id,
            version.DietRuleId,
            version.VersionNumber,
            version.ParametersJson,
            version.SourceUrl,
            version.SourceType,
            version.Citation,
            version.EvidenceLevel,
            version.State,
            version.EffectiveAt,
            version.PublishedAt);

    private static RuleSetDto ToRuleSetDto(RuleSetVersion ruleSet) =>
        new(
            ruleSet.Id,
            ruleSet.VersionLabel,
            ruleSet.IsActive,
            ruleSet.EffectiveAt,
            ruleSet.PublishedAt,
            ruleSet.RuleVersions
                .Where(x => x.State == RuleVersionState.Published)
                .OrderBy(x => x.DietRule.RuleKey)
                .ThenBy(x => x.VersionNumber)
                .Select(ToVersionDto)
                .ToList());
}
