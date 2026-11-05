using LongevityDiet.API.Errors;
using LongevityDiet.API.Infrastructure;
using LongevityDiet.API.Security;
using LongevityDiet.Services.Tracking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LongevityDiet.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/weekly-reports")]
public sealed class WeeklyReportsController(
    WeeklyReportService service,
    WeeklyReportStreamPublisher publisher,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WeeklyReportSummary>>> List(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId) return Unauthorized();
        var result = await service.ListAsync(userId, cancellationToken);
        return Ok(result.Value);
    }

    [HttpGet("current")]
    public async Task<ActionResult<WeeklyReportSummary>> Current(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId) return Unauthorized();
        var result = await service.GetCurrentAsync(userId, cancellationToken);
        return !result.Succeeded || result.Value is null
            ? this.ToProblem(result, StatusCodes.Status404NotFound)
            : Ok(result.Value);
    }

    [HttpGet("{reportId:guid}")]
    [ProducesResponseType<WeeklyReportSummary>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WeeklyReportSummary>> Get(Guid reportId, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId) return Unauthorized();
        var result = await service.GetAsync(userId, reportId, cancellationToken);
        return !result.Succeeded || result.Value is null ? this.ToProblem(result, StatusCodes.Status404NotFound) : Ok(result.Value);
    }

    [HttpPost("current/jobs")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> QueueCurrent(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId) return Unauthorized();
        var week = await service.GetCurrentWeekStartAsync(userId, cancellationToken);
        if (!week.Succeeded || week.Value == default) return this.ToProblem(week);

        try
        {
            var streamId = await publisher.PublishRequestedAsync(userId, week.Value);
            return Accepted(new { streamId, weekStart = week.Value });
        }
        catch (Exception ex) when (ex is StackExchange.Redis.RedisException or InvalidOperationException)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Weekly report queue unavailable",
                detail: ex.Message);
        }
    }
}
