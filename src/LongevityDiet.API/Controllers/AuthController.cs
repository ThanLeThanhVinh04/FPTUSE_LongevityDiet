using LongevityDiet.API.Contracts.Auth;
using LongevityDiet.API.Errors;
using LongevityDiet.Services.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace LongevityDiet.API.Controllers;

[ApiController]
[EnableRateLimiting("auth")]
[AllowAnonymous]
[Route("api/v1/auth")]
public sealed class AuthController(AuthenticationService authenticationService) : ControllerBase
{
    private const string RefreshCookieName = "ldc_refresh";

    [HttpPost("register")]
    [ProducesResponseType<AuthSessionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthSessionResponse>> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authenticationService.RegisterAsync(
            new RegisterUserCommand(request.Email, request.Password, request.DisplayName),
            cancellationToken);

        if (!result.Succeeded || result.Value is null)
        {
            return this.ToProblem(result);
        }

        SetRefreshCookie(result.Value);
        return StatusCode(StatusCodes.Status201Created, ToResponse(result.Value));
    }

    [HttpPost("login")]
    [ProducesResponseType<AuthSessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuthSessionResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authenticationService.LoginAsync(
            new LoginUserCommand(request.Email, request.Password),
            cancellationToken);

        if (!result.Succeeded || result.Value is null)
        {
            return this.ToProblem(result);
        }

        SetRefreshCookie(result.Value);
        return Ok(ToResponse(result.Value));
    }

    [HttpPost("refresh")]
    [ProducesResponseType<AuthSessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthSessionResponse>> Refresh(CancellationToken cancellationToken)
    {
        Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken);

        var result = await authenticationService.RefreshAsync(
            refreshToken ?? string.Empty,
            cancellationToken);

        if (!result.Succeeded || result.Value is null)
        {
            DeleteRefreshCookie();
            return this.ToProblem(result);
        }

        SetRefreshCookie(result.Value);
        return Ok(ToResponse(result.Value));
    }

    [HttpPost("revoke")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Revoke(CancellationToken cancellationToken)
    {
        Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken);
        await authenticationService.RevokeAsync(refreshToken, cancellationToken);

        DeleteRefreshCookie();
        return NoContent();
    }

    private void SetRefreshCookie(AuthenticatedSession session)
    {
        Response.Cookies.Append(
            RefreshCookieName,
            session.RefreshToken,
            CreateRefreshCookieOptions(session.RefreshTokenExpiresAt));
    }

    private void DeleteRefreshCookie()
    {
        Response.Cookies.Delete(
            RefreshCookieName,
            CreateRefreshCookieOptions(expires: null));
    }

    private CookieOptions CreateRefreshCookieOptions(DateTimeOffset? expires) =>
        new()
        {
            HttpOnly = true,
            // Fail closed for production deployments even behind a TLS proxy that
            // has not yet populated forwarded scheme headers.
            Secure = Request.IsHttps || !HttpContext.RequestServices
                .GetRequiredService<IHostEnvironment>().IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Path = "/api/v1/auth",
            Expires = expires,
            IsEssential = true,
        };

    private static AuthSessionResponse ToResponse(AuthenticatedSession session) =>
        new(
            session.AccessToken,
            session.AccessTokenExpiresAt,
            new SessionUserResponse(
                session.User.Id,
                session.User.Email,
                session.User.DisplayName,
                session.User.Role,
                session.User.ProfileCompleted));
}
