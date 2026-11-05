using LongevityDiet.API.Errors;
using LongevityDiet.Services.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LongevityDiet.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/foods")]
public sealed class FoodsController(FoodCatalogService foods) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<FoodDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<FoodDto>>> GetAll(
        [FromQuery] FoodQueryOptions query,
        CancellationToken cancellationToken)
    {
        var result = await foods.QueryAsync(query, cancellationToken: cancellationToken);
        return result.Succeeded && result.Value is not null
            ? Ok(result.Value)
            : this.ToProblem(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<FoodDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FoodDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await foods.GetAsync(id, cancellationToken: cancellationToken);
        return result.Succeeded && result.Value is not null
            ? Ok(result.Value)
            : this.ToProblem(result, StatusCodes.Status404NotFound);
    }
}
