using LongevityDiet.API.Errors;
using LongevityDiet.Services.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LongevityDiet.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/diet-rules")]
public sealed class DietRulesController(RuleCatalogService rules) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DietRuleDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<DietRuleDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await rules.GetActiveRulesAsync(cancellationToken);
        return Ok(result.Value);
    }

    [HttpGet("active-set")]
    [ProducesResponseType<RuleSetDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RuleSetDto>> GetActiveSet(CancellationToken cancellationToken)
    {
        var result = await rules.GetActiveRuleSetAsync(cancellationToken);
        return result.Succeeded && result.Value is not null
            ? Ok(result.Value)
            : this.ToProblem(result, StatusCodes.Status404NotFound);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<DietRuleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DietRuleDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await rules.GetAsync(id, cancellationToken: cancellationToken);
        return result.Succeeded && result.Value is not null
            ? Ok(result.Value)
            : this.ToProblem(result, StatusCodes.Status404NotFound);
    }
}
