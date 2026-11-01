using LongevityDiet.API.Contracts.Tracking;
using LongevityDiet.API.Errors;
using LongevityDiet.API.Security;
using LongevityDiet.Services.Tracking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LongevityDiet.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/eating-window")]
public sealed class EatingWindowController(
    MealTrackingService mealTrackingService,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("summary")]
    [ProducesResponseType<EatingWindowSummaryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<EatingWindowSummaryResponse>> GetSummary(
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await mealTrackingService.GetEatingWindowSummaryAsync(
            userId,
            date,
            cancellationToken);

        if (!result.Succeeded || result.Value is null)
        {
            return this.ToProblem(result);
        }

        return Ok(ToResponse(result.Value));
    }

    [HttpGet("range")]
    [ProducesResponseType<IReadOnlyList<EatingWindowSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<EatingWindowSummaryResponse>>> GetRange(
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await mealTrackingService.ListEatingWindowSummariesAsync(
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

    private static EatingWindowSummaryResponse ToResponse(EatingWindowSummary summary) =>
        new(
            summary.UserId,
            summary.LocalDate,
            summary.TimeZone,
            summary.LoggedMealCount,
            summary.CaloricMealCount,
            summary.TotalCaloriesKcal,
            summary.FirstMealAt,
            summary.LastMealAt,
            summary.FirstMealLocalTime,
            summary.LastMealLocalTime,
            summary.EatingWindowMinutes,
            summary.TargetWindowMinutes,
            summary.IsWithinTargetWindow,
            summary.ConfiguredSleepTime,
            summary.MinutesBeforeBedtime,
            summary.IsLateEatingFlagged,
            summary.StatusCode,
            summary.SummaryMessage,
            summary.CalculatedAt);
}

