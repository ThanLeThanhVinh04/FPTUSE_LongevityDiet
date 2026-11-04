using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LongevityDiet.Domain.Entities;
using LongevityDiet.Repositories.Repositories;
using Microsoft.Extensions.Options;

namespace LongevityDiet.API.Cloudflare;

/// <summary>
/// Opt-in remote refresh-session repository backed by a private Cloudflare Worker/D1.
/// Never transmits a raw refresh token; only the SHA-256 token hash.
/// SQL remains the default provider unless CloudflareSessions:Enabled is explicitly set.
/// </summary>
public sealed class CloudflareRefreshTokenRepository(
    HttpClient http,
    IUserRepository users,
    IOptions<CloudflareSessionOptions> options) : IRefreshTokenRepository
{
    private readonly string _serviceBearer = options.Value.ServiceBearer;
    private RefreshToken? _loaded;
    private Guid _loadedFamilyId;
    private RefreshToken? _pending;

    public async Task<RefreshToken?> FindByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            "/v1/sessions/lookup",
            new { tokenHash },
            cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var record = await response.Content.ReadFromJsonAsync<LookupReply>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Cloudflare session lookup returned no body.");

        var user = await users.FindByIdAsync(record.UserId, cancellationToken);
        if (user is null) { return null; }

        _loadedFamilyId = record.FamilyId;
        _loaded = new RefreshToken
        {
            Id = record.Id,
            UserId = user.Id,
            User = user,
            TokenHash = tokenHash,
            ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(record.ExpiresAt),
        };
        return _loaded;
    }

    public Task AddAsync(
        RefreshToken token,
        CancellationToken cancellationToken = default)
    {
        if (_pending is not null)
        {
            throw new InvalidOperationException("A second refresh session cannot be staged in the same request.");
        }

        _pending = token;
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (_loaded is not null && _pending is not null)
        {
            // Caller already checked User.Status and token expiry. Remote D1
            // atomic rotation still enforces one-use and validates the old hash.
            using var response = await SendAsync(
                "/v1/sessions/rotate",
                new
                {
                    oldTokenHash = _loaded.TokenHash,
                    newTokenHash = _pending.TokenHash,
                    newId = _pending.Id,
                    expiresAt = _pending.ExpiresAt.ToUnixTimeSeconds(),
                },
                cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        else if (_loaded is not null && _loaded.RevokedAt is not null)
        {
            using var response = await SendAsync(
                "/v1/sessions/revoke",
                new { tokenHash = _loaded.TokenHash },
                cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        else if (_pending is not null)
        {
            using var response = await SendAsync(
                "/v1/sessions/issue",
                new
                {
                    id = _pending.Id,
                    userId = _pending.UserId,
                    tokenHash = _pending.TokenHash,
                    familyId = _loadedFamilyId == Guid.Empty ? Guid.NewGuid() : _loadedFamilyId,
                    createdAt = _pending.CreatedAt.ToUnixTimeSeconds(),
                    expiresAt = _pending.ExpiresAt.ToUnixTimeSeconds(),
                },
                cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        _loaded = null;
        _loadedFamilyId = Guid.Empty;
        _pending = null;
    }

    private async Task<HttpResponseMessage> SendAsync(
        string path,
        object payload,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _serviceBearer);
        // No logging of payloads: they contain session hashes (never raw tokens).
        return await http.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
    }

    private sealed record LookupReply(
        Guid Id,
        Guid UserId,
        Guid FamilyId,
        long ExpiresAt);
}
