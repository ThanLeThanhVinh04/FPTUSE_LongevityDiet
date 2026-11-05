using LongevityDiet.API.Contracts.Catalog;
using LongevityDiet.API.Errors;
using LongevityDiet.API.Security;
using LongevityDiet.Services.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LongevityDiet.API.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Route("api/v1/admin")]
public sealed class AdminRulesController(RuleCatalogService rules) : ControllerBase
{
    [HttpGet("diet-rules")]
    [ProducesResponseType<IReadOnlyList<DietRuleDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<DietRuleDto>>> GetRules(
        CancellationToken cancellationToken)
    {
        var result = await rules.GetAllRulesAsync(cancellationToken);
        return Ok(result.Value);
    }

    [HttpPost("diet-rules")]
    [ProducesResponseType<DietRuleDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DietRuleDto>> CreateRule(
        CreateDietRuleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await rules.CreateRuleAsync(ToCommand(request), cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            return this.ToProblem(result);
        }

        return Created($"/api/v1/diet-rules/{result.Value.Id}", result.Value);
    }

    [HttpPut("diet-rules/{id:guid}")]
    [ProducesResponseType<DietRuleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DietRuleDto>> UpdateRule(
        Guid id,
        CreateDietRuleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await rules.UpdateRuleAsync(id, ToCommand(request), cancellationToken);
        return result.Succeeded && result.Value is not null ? Ok(result.Value) : this.ToProblem(result);
    }

    [HttpPost("diet-rules/{id:guid}/versions")]
    [ProducesResponseType<RuleVersionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RuleVersionDto>> CreateVersion(
        Guid id,
        SaveRuleVersionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await rules.CreateVersionAsync(id, ToCreateVersionCommand(request), cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            return this.ToProblem(result);
        }

        return Created($"/api/v1/admin/rule-versions/{result.Value.Id}", result.Value);
    }

    [HttpPut("rule-versions/{id:guid}")]
    [ProducesResponseType<RuleVersionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RuleVersionDto>> UpdateVersion(
        Guid id,
        SaveRuleVersionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await rules.UpdateVersionAsync(id, ToUpdateVersionCommand(request), cancellationToken);
        return result.Succeeded && result.Value is not null ? Ok(result.Value) : this.ToProblem(result);
    }

    [HttpPost("rule-versions/{id:guid}/publication")]
    [ProducesResponseType<RuleVersionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RuleVersionDto>> PublishVersion(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await rules.PublishVersionAsync(id, cancellationToken);
        return result.Succeeded && result.Value is not null ? Ok(result.Value) : this.ToProblem(result);
    }

    private static CreateDietRuleCommand ToCommand(CreateDietRuleRequest request) =>
        new(request.RuleKey, request.Title, request.Description, request.Category, request.IsMedicalSensitive);

    private static CreateRuleVersionCommand ToCreateVersionCommand(SaveRuleVersionRequest request) =>
        new(request.ParametersJson, request.SourceUrl, request.SourceType, request.Citation, request.EvidenceLevel, request.EffectiveAt);

    private static UpdateRuleVersionCommand ToUpdateVersionCommand(SaveRuleVersionRequest request) =>
        new(request.ParametersJson, request.SourceUrl, request.SourceType, request.Citation, request.EvidenceLevel, request.EffectiveAt);
}
