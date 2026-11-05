using LongevityDiet.API.Contracts.Catalog;
using LongevityDiet.API.Errors;
using LongevityDiet.API.Security;
using LongevityDiet.Services.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LongevityDiet.API.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Route("api/v1/admin/recipes")]
public sealed class AdminRecipesController(RecipeCatalogService recipes) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<RecipeSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<RecipeSummaryDto>>> GetAll(
        [FromQuery] RecipeQueryOptions query,
        CancellationToken cancellationToken)
    {
        var result = await recipes.QueryAsync(query, includeInactive: true, cancellationToken);
        return result.Succeeded && result.Value is not null ? Ok(result.Value) : this.ToProblem(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<RecipeDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecipeDetailDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await recipes.GetAsync(id, includeInactive: true, cancellationToken);
        return result.Succeeded && result.Value is not null ? Ok(result.Value) : this.ToProblem(result);
    }

    [HttpPost]
    [ProducesResponseType<RecipeDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RecipeDetailDto>> Create(
        SaveRecipeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await recipes.CreateAsync(ToCommand(request), cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            return this.ToProblem(result);
        }

        return Created($"/api/v1/recipes/{result.Value.Id}", result.Value);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<RecipeDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecipeDetailDto>> Update(
        Guid id,
        SaveRecipeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await recipes.UpdateAsync(id, ToCommand(request), cancellationToken);
        return result.Succeeded && result.Value is not null ? Ok(result.Value) : this.ToProblem(result);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var result = await recipes.DeactivateAsync(id, cancellationToken);
        return result.Succeeded ? NoContent() : this.ToProblem(result);
    }

    private static SaveRecipeCommand ToCommand(SaveRecipeRequest request) =>
        new(
            request.Name, request.Description, request.Category, request.Cuisine, request.Tags,
            request.ServingCount, request.PrepMinutes, request.CaloriesPerServing,
            request.ProteinGPerServing, request.CarbohydrateGPerServing, request.FatGPerServing,
            request.SaturatedFatGPerServing, request.SugarGPerServing, request.FiberGPerServing,
            request.IsPlantForward, request.HasLegume, request.HasWholeGrain, request.HasFish,
            request.Ingredients
                .Select(x => new RecipeIngredientInput(x.FoodId, x.Amount, x.Unit, x.IsOptional))
                .ToList(),
            request.AllergenIds);
}
