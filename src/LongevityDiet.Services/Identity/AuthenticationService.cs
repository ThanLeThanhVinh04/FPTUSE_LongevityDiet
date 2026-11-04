using System.Security.Cryptography;
using System.Text;
using LongevityDiet.Domain.Entities;
using LongevityDiet.Repositories.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace LongevityDiet.Services.Identity;

public sealed class AuthenticationService(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher<User> passwordHasher,
    IAccessTokenProvider accessTokenProvider,
    IOptions<AuthenticationOptions> options,
    TimeProvider timeProvider)
{
    private readonly AuthenticationOptions _options = options.Value;

    public async Task<ServiceResult<AuthenticatedSession>> RegisterAsync(
        RegisterUserCommand command,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(command.Email);

        if (await users.EmailExistsAsync(normalizedEmail, cancellationToken))
        {
            return ServiceResult.Failure<AuthenticatedSession>(
                "email_exists",
                "An account with this email already exists.");
        }

        if (!IsStrongEnoughPassword(command.Password))
        {
            return ServiceResult.Failure<AuthenticatedSession>(
                "weak_password",
                "Password must be at least 10 characters and include upper-case, lower-case and numeric characters.");
        }

        var now = timeProvider.GetUtcNow();
        var user = new User
        {
            Email = command.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            DisplayName = command.DisplayName.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
            Profile = new UserProfile
            {
                TimeZone = "Asia/Ho_Chi_Minh",
                PreferredMealFrequency = 3,
                ProfileCompleted = false,
                UpdatedAt = now,
            },
        };

        user.Profile.UserId = user.Id;
        user.Profile.User = user;
        user.PasswordHash = passwordHasher.HashPassword(user, command.Password);

        await users.AddAsync(user, cancellationToken);

        var session = CreateSession(user, now);
        await refreshTokens.AddAsync(session.TokenEntity, cancellationToken);
        await users.SaveChangesAsync(cancellationToken);
        // SQL provider has already committed through the shared DbContext.
        // Cloudflare provider writes the session only after User is persisted.
        await refreshTokens.SaveChangesAsync(cancellationToken);

        return ServiceResult.Success(session.Session);
    }

    public async Task<ServiceResult<AuthenticatedSession>> LoginAsync(
        LoginUserCommand command,
        CancellationToken cancellationToken = default)
    {
        var user = await users.FindByEmailAsync(
            NormalizeEmail(command.Email),
            cancellationToken);

        if (user is null)
        {
            return InvalidCredentials();
        }

        if (user.Status != AccountStatus.Active)
        {
            return ServiceResult.Failure<AuthenticatedSession>(
                "account_disabled",
                "This account is disabled.");
        }

        var verification = passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            command.Password);

        if (verification == PasswordVerificationResult.Failed)
        {
            return InvalidCredentials();
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, command.Password);
            user.UpdatedAt = timeProvider.GetUtcNow();
        }

        var now = timeProvider.GetUtcNow();
        var session = CreateSession(user, now);

        await refreshTokens.AddAsync(session.TokenEntity, cancellationToken);
        await users.SaveChangesAsync(cancellationToken);
        // SQL provider has already committed through the shared DbContext.
        // Cloudflare provider writes the session only after User is persisted.
        await refreshTokens.SaveChangesAsync(cancellationToken);

        return ServiceResult.Success(session.Session);
    }

    public async Task<ServiceResult<AuthenticatedSession>> RefreshAsync(
        string rawRefreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return InvalidRefreshToken();
        }

        var current = await refreshTokens.FindByHashAsync(
            HashToken(rawRefreshToken),
            cancellationToken);

        var now = timeProvider.GetUtcNow();

        if (current is null ||
            !current.IsActive(now) ||
            current.User.Status != AccountStatus.Active)
        {
            return InvalidRefreshToken();
        }

        var next = CreateSession(current.User, now);
        current.RevokedAt = now;
        current.ReplacedByTokenId = next.TokenEntity.Id;

        await refreshTokens.AddAsync(next.TokenEntity, cancellationToken);
        await refreshTokens.SaveChangesAsync(cancellationToken);

        return ServiceResult.Success(next.Session);
    }

    public async Task RevokeAsync(
        string? rawRefreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return;
        }

        var token = await refreshTokens.FindByHashAsync(
            HashToken(rawRefreshToken),
            cancellationToken);

        if (token is null || token.RevokedAt is not null)
        {
            return;
        }

        token.RevokedAt = timeProvider.GetUtcNow();
        await refreshTokens.SaveChangesAsync(cancellationToken);
    }

    private (AuthenticatedSession Session, RefreshToken TokenEntity) CreateSession(
        User user,
        DateTimeOffset now)
    {
        var accessToken = accessTokenProvider.Create(user);
        var rawRefreshToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var refreshExpiresAt = now.AddDays(Math.Max(1, _options.RefreshTokenDays));

        var tokenEntity = new RefreshToken
        {
            UserId = user.Id,
            User = user,
            TokenHash = HashToken(rawRefreshToken),
            CreatedAt = now,
            ExpiresAt = refreshExpiresAt,
        };

        var session = new AuthenticatedSession(
            ToUserSummary(user),
            accessToken.Token,
            accessToken.ExpiresAt,
            rawRefreshToken,
            refreshExpiresAt);

        return (session, tokenEntity);
    }

    private static string NormalizeEmail(string email) =>
        email.Trim().ToUpperInvariant();

    private static string HashToken(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    private static bool IsStrongEnoughPassword(string password) =>
        password.Length >= 10 &&
        password.Any(char.IsUpper) &&
        password.Any(char.IsLower) &&
        password.Any(char.IsDigit);

    private static UserSummary ToUserSummary(User user) =>
        new(
            user.Id,
            user.Email,
            user.DisplayName,
            user.Role.ToString(),
            user.Profile?.ProfileCompleted ?? false);

    private static ServiceResult<AuthenticatedSession> InvalidCredentials() =>
        ServiceResult.Failure<AuthenticatedSession>(
            "invalid_credentials",
            "Email or password is incorrect.");

    private static ServiceResult<AuthenticatedSession> InvalidRefreshToken() =>
        ServiceResult.Failure<AuthenticatedSession>(
            "invalid_refresh_token",
            "The refresh session is invalid or expired.");
}
