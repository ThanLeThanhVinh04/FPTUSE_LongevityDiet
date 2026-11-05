using LongevityDiet.API.Contracts.Tracking;
using LongevityDiet.API.Errors;
using LongevityDiet.API.Security;
using LongevityDiet.Services.Tracking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LongevityDiet.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/challenges")]
public sealed class ChallengesController(ChallengeProgressService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("templates")]
    [ProducesResponseType<IReadOnlyList<ChallengeTemplateSummary>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ChallengeTemplateSummary>>> ListTemplates(CancellationToken cancellationToken)
    {
        var result = await service.ListTemplatesAsync(cancellationToken);
        return Ok(result.Value);
    }

    [HttpPost]
    [ProducesResponseType<UserChallengeSummary>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserChallengeSummary>> Start(StartChallengeRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId) return Unauthorized();
        var result = await service.StartAsync(userId, request.TemplateId, request.StartDate, cancellationToken);
        if (!result.Succeeded || result.Value is null)
            return result.ErrorCode == "active_challenge_exists"
                ? this.ToProblem(result, StatusCodes.Status409Conflict)
                : this.ToProblem(result);
        return Created($"/api/v1/challenges/{result.Value.Id}", result.Value);
    }

    [HttpGet("current")]
    [ProducesResponseType<UserChallengeSummary>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserChallengeSummary>> GetCurrent(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId) return Unauthorized();
        var result = await service.GetCurrentAsync(userId, cancellationToken);
        return !result.Succeeded || result.Value is null ? this.ToProblem(result, StatusCodes.Status404NotFound) : Ok(result.Value);
    }

    [HttpGet("history")]
    public async Task<ActionResult<IReadOnlyList<UserChallengeSummary>>> History(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId) return Unauthorized();
        var result = await service.ListHistoryAsync(userId, cancellationToken);
        return Ok(result.Value);
    }

    [HttpPut("{challengeId:guid}/days/{dayNumber:int}")]
    [ProducesResponseType<UserChallengeSummary>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserChallengeSummary>> SetDay(
        Guid challengeId,
        int dayNumber,
        SetChallengeDayCompletionRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId) return Unauthorized();
        var result = await service.SetDayCompletionAsync(userId, challengeId, dayNumber, request.Completed, cancellationToken);
        if (!result.Succeeded || result.Value is null)
            return result.ErrorCode == "challenge_not_found"
                ? this.ToProblem(result, StatusCodes.Status404NotFound)
                : this.ToProblem(result);
        return Ok(result.Value);
    }
}
