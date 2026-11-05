using LongevityDiet.API.Contracts.Fmd;
using LongevityDiet.API.Errors;
using LongevityDiet.API.Security;
using LongevityDiet.Services.Fmd;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LongevityDiet.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/fmd")]
public sealed class FmdController(
    FmdService fmdService,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("education")]
    [AllowAnonymous]
    public IActionResult GetEducation()
    {
        var content = FmdService.GetEducationContent();
        var questions = FmdSafetyCatalog.ScreeningQuestions;
        return Ok(new
        {
            Education = content,
            ScreeningQuestions = questions,
        });
    }

    [HttpGet("safety/status")]
    public async Task<IActionResult> GetSafetyStatus(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await fmdService.GetSafetyStatusAsync(userId, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.ToProblem(result);
    }

    [HttpPost("education/acknowledge")]
    public async Task<IActionResult> AcknowledgeEducation(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await fmdService.AcknowledgeEducationAsync(userId, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.ToProblem(result);
    }

    [HttpPost("safety/assessment")]
    public async Task<IActionResult> SubmitAssessment(
        [FromBody] SubmitFmdAssessmentRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await fmdService.SubmitAssessmentAsync(
            userId,
            new SubmitFmdAssessmentCommand(request.Answers, request.Notes),
            cancellationToken);

        return result.Succeeded ? Ok(result.Value) : this.ToProblem(result);
    }

    [HttpGet("cycles/active")]
    public async Task<IActionResult> GetActiveCycle(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await fmdService.GetActiveCycleAsync(userId, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.ToProblem(result);
    }

    [HttpPost("cycles")]
    public async Task<IActionResult> StartCycle(
        [FromBody] StartFmdCycleRequest? request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await fmdService.StartCycleAsync(
            userId,
            new StartFmdCycleCommand(request?.StartDate, request?.Notes),
            cancellationToken);

        if (!result.Succeeded || result.Value is null)
        {
            return this.ToProblem(result);
        }

        return CreatedAtAction(nameof(GetActiveCycle), new { cycleId = result.Value.Id }, result.Value);
    }

    [HttpPost("cycles/{cycleId:guid}/days/{dayNumber:int}")]
    public async Task<IActionResult> LogCycleDay(
        Guid cycleId,
        int dayNumber,
        [FromBody] LogFmdCycleDayRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await fmdService.LogCycleDayAsync(
            userId,
            cycleId,
            new LogFmdCycleDayCommand(
                dayNumber,
                request.EnergyLevel,
                request.HungerLevel,
                request.WaterIntakeLiters,
                request.Symptoms,
                request.DailyNotes),
            cancellationToken);

        return result.Succeeded ? Ok(result.Value) : this.ToProblem(result);
    }

    [HttpPost("cycles/{cycleId:guid}/stop")]
    public async Task<IActionResult> StopCycle(
        Guid cycleId,
        [FromBody] StopFmdCycleRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await fmdService.StopCycleAsync(
            userId,
            cycleId,
            new StopFmdCycleCommand(request.StopReason),
            cancellationToken);

        return result.Succeeded ? Ok(result.Value) : this.ToProblem(result);
    }

    [HttpPost("cycles/{cycleId:guid}/complete")]
    public async Task<IActionResult> CompleteCycle(
        Guid cycleId,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await fmdService.CompleteCycleAsync(userId, cycleId, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.ToProblem(result);
    }

    [HttpGet("cycles")]
    public async Task<IActionResult> ListCycles(
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await fmdService.ListCyclesAsync(userId, limit, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.ToProblem(result);
    }
}

