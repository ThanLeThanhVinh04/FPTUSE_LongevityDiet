using LongevityDiet.API.Contracts.Catalog;
using LongevityDiet.API.Errors;
using LongevityDiet.API.Security;
using LongevityDiet.Services.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LongevityDiet.API.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Route("api/v1/admin/foods")]
public sealed class AdminFoodsController(FoodCatalogService foods) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<FoodDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<FoodDto>>> GetAll(
        [FromQuery] FoodQueryOptions query,
        CancellationToken cancellationToken)
    {
        var result = await foods.QueryAsync(query, includeInactive: true, cancellationToken);
        return result.Succeeded && result.Value is not null ? Ok(result.Value) : this.ToProblem(result);
    }

    [HttpPost]
    [ProducesResponseType<FoodDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<FoodDto>> Create(
        SaveFoodRequest request,
        CancellationToken cancellationToken)
    {
        var result = await foods.CreateAsync(ToCommand(request), cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            return this.ToProblem(result);
        }

        return Created($"/api/v1/foods/{result.Value.Id}", result.Value);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<FoodDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FoodDto>> Update(
        Guid id,
        SaveFoodRequest request,
        CancellationToken cancellationToken)
    {
        var result = await foods.UpdateAsync(id, ToCommand(request), cancellationToken);
        return result.Succeeded && result.Value is not null ? Ok(result.Value) : this.ToProblem(result);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var result = await foods.DeactivateAsync(id, cancellationToken);
        return result.Succeeded ? NoContent() : this.ToProblem(result);
    }

    private static SaveFoodCommand ToCommand(SaveFoodRequest request) =>
        new(
            request.Name, request.Category, request.Tags, request.Cuisine,
            request.DefaultServingAmount, request.ServingUnit, request.Calories,
            request.ProteinG, request.CarbohydrateG, request.FatG,
            request.SaturatedFatG, request.SugarG, request.FiberG,
            request.IsPlantBased, request.IsLegume, request.IsWholeGrain, request.IsFish);
}
