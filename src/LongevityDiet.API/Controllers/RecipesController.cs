using LongevityDiet.API.Errors;
using LongevityDiet.Services.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LongevityDiet.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/recipes")]
public sealed class RecipesController(RecipeCatalogService recipes) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<RecipeSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<RecipeSummaryDto>>> GetAll(
        [FromQuery] RecipeQueryOptions query,
        CancellationToken cancellationToken)
    {
        var result = await recipes.QueryAsync(query, cancellationToken: cancellationToken);
        return result.Succeeded && result.Value is not null
            ? Ok(result.Value)
            : this.ToProblem(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<RecipeDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecipeDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await recipes.GetAsync(id, cancellationToken: cancellationToken);
        return result.Succeeded && result.Value is not null
            ? Ok(result.Value)
            : this.ToProblem(result, StatusCodes.Status404NotFound);
    }
}
