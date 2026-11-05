using LongevityDiet.API.Errors;
using LongevityDiet.API.Security;
using LongevityDiet.Services.Tracking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LongevityDiet.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/dashboard")]
public sealed class DashboardController(ChallengeProgressService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("summary")]
    [ProducesResponseType<DashboardSummary>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DashboardSummary>> GetSummary(
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId) return Unauthorized();
        var result = await service.GetDashboardAsync(userId, fromDate, toDate, cancellationToken);
        return !result.Succeeded || result.Value is null ? this.ToProblem(result) : Ok(result.Value);
    }
}
