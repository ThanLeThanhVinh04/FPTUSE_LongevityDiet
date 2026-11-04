using LongevityDiet.API.Security;
using LongevityDiet.Tracking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace LongevityDiet.API.Controllers;

[ApiController]
[EnableRateLimiting("member-api")]
[Authorize]
[Route("api/v1")]
public sealed class TrackingController(
    TrackingService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("activities")]
    [ProducesResponseType<ActivityView>(StatusCodes.Status201Created)]
    public Task<IActionResult> Create(ActivityInput request, CancellationToken ct) =>
        Handle(async userId =>
        {
            var created = await service.CreateAsync(userId, request, ct);
            return Created($"/api/v1/activities/{created.Id}", created);
        });

    [HttpPut("activities/{id:guid}")]
    public Task<IActionResult> Update(Guid id, ActivityInput request, CancellationToken ct) =>
        Handle(async userId =>
        {
            var result = await service.UpdateAsync(userId, id, request, ct);
            return result is null ? NotFound() : Ok(result);
        });

    [HttpDelete("activities/{id:guid}")]
    public Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        Handle(async userId => await service.DeleteAsync(userId, id, ct)
            ? NoContent() : NotFound());

    [HttpGet("activities")]
    public Task<IActionResult> List(
        DateTimeOffset? from, DateTimeOffset? to,
        int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        Handle(async userId => Ok(await service.ListAsync(userId, from, to, page, pageSize, ct)));

    [HttpGet("activities/daily")]
    public Task<IActionResult> Daily(DateOnly? date, CancellationToken ct) =>
        Handle(async userId => Ok(await service.GetDailySummaryAsync(userId, date, ct)));

    [HttpGet("activities/summary")]
    public Task<IActionResult> Summary(DateOnly? weekStart, CancellationToken ct) =>
        Handle(async userId => Ok(await service.GetWeeklySummaryAsync(userId, weekStart, ct)));

    [HttpGet("activities/goal")]
    public Task<IActionResult> Goal(CancellationToken ct) =>
        Handle(async userId => Ok(await service.GetGoalAsync(userId, ct)));

    [HttpPut("activities/goal")]
    public Task<IActionResult> UpdateGoal(GoalInput request, CancellationToken ct) =>
        Handle(async userId => Ok(await service.SetGoalAsync(userId, request, ct)));

    [HttpGet("ldas/latest")]
    public Task<IActionResult> Latest(CancellationToken ct) =>
        Handle(async userId => Ok(await service.GetLatestScoreAsync(userId, ct)));

    [HttpGet("ldas/{date}")]
    public Task<IActionResult> Score(DateOnly date, CancellationToken ct) =>
        Handle(async userId => Ok(await service.GetScoreAsync(userId, date, ct)));

    [HttpGet("ldas/history")]
    public Task<IActionResult> History(int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        Handle(async userId => Ok(await service.HistoryAsync(userId, page, pageSize, ct)));

    private async Task<IActionResult> Handle(Func<Guid, Task<IActionResult>> callback)
    {
        if (currentUser.UserId is not { } userId) return Unauthorized();
        try { return await callback(userId); }
        catch (TrackingInputException error)
        {
            return Problem(
                statusCode: error.Code == "inactive_user" ? 403 : 400,
                title: error.Code,
                detail: error.Message);
        }
    }
}
