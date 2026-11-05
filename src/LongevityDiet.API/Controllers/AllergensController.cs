using LongevityDiet.Services.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LongevityDiet.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/allergens")]
public sealed class AllergensController(RecipeCatalogService recipes) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AllergenDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<AllergenDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        var result = await recipes.GetActiveAllergensAsync(cancellationToken);
        return Ok(result.Value);
    }
}
