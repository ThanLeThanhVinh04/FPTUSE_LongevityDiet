using LongevityDiet.API.Security;
using LongevityDiet.Services.Reminders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace LongevityDiet.API.Controllers;

[ApiController]
[EnableRateLimiting("member-api")]
[Authorize]
[Route("api/v1")]
public sealed class ReminderNotificationsController(
    ReminderService service,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("reminders/preferences")]
    public Task<IActionResult> GetPreferences(CancellationToken ct) =>
        Handle(async userId => Ok(await service.GetPreferencesAsync(userId, ct)));

    [HttpPut("reminders/preferences")]
    public Task<IActionResult> SetPreferences(
        ReminderPreferencesInput request, CancellationToken ct) =>
        Handle(async userId => Ok(await service.UpdatePreferencesAsync(userId, request, ct)));

    [HttpGet("reminders/preview")]
    public Task<IActionResult> Preview(CancellationToken ct) =>
        Handle(async userId => Ok(await service.PreviewAsync(userId, ct)));

    [HttpGet("notifications")]
    public Task<IActionResult> Notifications(int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        Handle(async userId => Ok(await service.ListAsync(userId, page, pageSize, ct)));

    [HttpGet("notifications/unread-count")]
    public Task<IActionResult> UnreadCount(CancellationToken ct) =>
        Handle(async userId => Ok(new { count = await service.UnreadCountAsync(userId, ct) }));

    [HttpPost("notifications/{id:guid}/read")]
    public Task<IActionResult> MarkRead(Guid id, CancellationToken ct) =>
        Handle(async userId => await service.MarkReadAsync(userId, id, ct)
            ? NoContent() : NotFound());

    [HttpPost("notifications/read-all")]
    public Task<IActionResult> MarkAllRead(CancellationToken ct) =>
        Handle(async userId => Ok(new { updated = await service.MarkAllReadAsync(userId, ct) }));

    private async Task<IActionResult> Handle(Func<Guid, Task<IActionResult>> action)
    {
        if (currentUser.UserId is not { } userId) return Unauthorized();
        try { return await action(userId); }
        catch (ReminderAccessException ex)
        {
            return Problem(statusCode: ex.Code == "inactive_user" ? 403 : 400,
                title: ex.Code, detail: ex.Message);
        }
    }
}
