using LongevityDiet.API.Contracts.Tracking;
using LongevityDiet.API.Errors;
using LongevityDiet.API.Security;
using LongevityDiet.Services.Tracking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LongevityDiet.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/meal-logs")]
public sealed class MealLogsController(
    MealTrackingService mealTrackingService,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<MealLogResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<MealLogResponse>>> List(
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await mealTrackingService.ListMealLogsAsync(
            userId,
            fromDate,
            toDate,
            cancellationToken);

        if (!result.Succeeded || result.Value is null)
        {
            return this.ToProblem(result);
        }

        return Ok(result.Value.Select(ToResponse).ToList());
    }

    [HttpPost]
    [ProducesResponseType<MealLogResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<MealLogResponse>> Create(
        CreateMealLogRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await mealTrackingService.CreateMealLogAsync(
            userId,
            new CreateMealLogCommand(
                request.EatenAt,
                request.MealType,
                request.Title,
                request.PlannedMealId,
                request.Notes,
                request.Items
                    .Select(x => new MealLogItemInput(
                        x.FoodCode,
                        x.FoodName,
                        x.ServingAmount,
                        x.ServingUnit,
                        x.CaloriesKcal,
                        x.ProteinGrams,
                        x.CarbsGrams,
                        x.FatGrams,
                        x.FiberGrams,
                        x.IsPlantBased))
                    .ToList()),
            cancellationToken);

        if (!result.Succeeded || result.Value is null)
        {
            return this.ToProblem(result);
        }

        var response = ToResponse(result.Value);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPut("{logId:guid}")]
    [ProducesResponseType<MealLogResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MealLogResponse>> Update(
        Guid logId,
        UpdateMealLogRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await mealTrackingService.UpdateMealLogAsync(
            userId,
            logId,
            new UpdateMealLogCommand(
                request.EatenAt,
                request.MealType,
                request.Title,
                request.PlannedMealId,
                request.Notes,
                request.Items
                    .Select(x => new MealLogItemInput(
                        x.FoodCode,
                        x.FoodName,
                        x.ServingAmount,
                        x.ServingUnit,
                        x.CaloriesKcal,
                        x.ProteinGrams,
                        x.CarbsGrams,
                        x.FatGrams,
                        x.FiberGrams,
                        x.IsPlantBased))
                    .ToList()),
            cancellationToken);

        if (!result.Succeeded || result.Value is null)
        {
            return this.ToProblem(result);
        }

        return Ok(ToResponse(result.Value));
    }

    [HttpDelete("{logId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid logId,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await mealTrackingService.DeleteMealLogAsync(userId, logId, cancellationToken);
        if (!result.Succeeded)
        {
            return this.ToProblem(result, StatusCodes.Status404NotFound);
        }

        return NoContent();
    }

    private static MealLogResponse ToResponse(MealLogSummary summary) =>
        new(
            summary.Id,
            summary.UserId,
            summary.PlannedMealId,
            summary.EatenAt,
            summary.LocalDate,
            summary.LocalTime,
            summary.TimeZone,
            summary.MealType,
            summary.Title,
            summary.Notes,
            summary.TotalCaloriesKcal,
            summary.TotalProteinGrams,
            summary.TotalCarbsGrams,
            summary.TotalFatGrams,
            summary.TotalFiberGrams,
            summary.CreatedAt,
            summary.UpdatedAt,
            summary.Items
                .Select(x => new MealLogItemResponse(
                    x.Id,
                    x.FoodCode,
                    x.FoodName,
                    x.ServingAmount,
                    x.ServingUnit,
                    x.CaloriesKcal,
                    x.ProteinGrams,
                    x.CarbsGrams,
                    x.FatGrams,
                    x.FiberGrams,
                    x.IsPlantBased))
                .ToList());
}

