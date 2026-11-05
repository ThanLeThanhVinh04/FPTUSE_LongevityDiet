using LongevityDiet.API.Security;
using LongevityDiet.Services.Tracking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LongevityDiet.API.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Route("api/v1/admin/worker")]
public sealed class WorkerOperationsController(WorkerReliabilityService service) : ControllerBase
{
    [HttpGet("diagnostics")]
    public async Task<ActionResult<WorkerDiagnosticsSummary>> Diagnostics(CancellationToken cancellationToken) =>
        Ok(await service.GetDiagnosticsAsync(cancellationToken));

    [HttpPost("dead-letters/{id:guid}/recovery")]
    public async Task<IActionResult> Recover(Guid id, CancellationToken cancellationToken)
    {
        var result = await service.RecoverDeadLetterAsync(id, cancellationToken);
        return result.Succeeded ? Accepted() : NotFound();
    }
}
