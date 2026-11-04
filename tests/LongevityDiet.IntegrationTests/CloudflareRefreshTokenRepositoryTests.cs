using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LongevityDiet.API.Cloudflare;
using LongevityDiet.Domain.Entities;
using LongevityDiet.Repositories.Repositories;
using Microsoft.Extensions.Options;

namespace LongevityDiet.IntegrationTests;

public sealed class CloudflareRefreshTokenRepositoryTests
{
    private const string Bearer = "unit-test-only-64-character-service-credential-never-real-secret";
    private static readonly Guid UserId = Guid.Parse("123e4567-e89b-42d3-a456-426614174000");
    private static readonly Guid FamilyId = Guid.Parse("123e4567-e89b-42d3-a456-426614174004");

    [Fact]
    public async Task NewSession_IssuesOnlyTheHashViaAuthenticatedWorkerCall()
    {
        var handler = new FakeWorkerHandler();
        var repository = CreateRepository(handler);
        var entity = NewToken('A');

        await repository.AddAsync(entity);
        await repository.SaveChangesAsync();

        var sent = Assert.Single(handler.Records);
        Assert.Equal("/v1/sessions/issue", sent.Path);
        Assert.Equal("Bearer " + Bearer, sent.Authorization);
        Assert.Equal(entity.TokenHash, sent.Body.GetProperty("tokenHash").GetString());
        Assert.False(sent.Body.TryGetProperty("rawRefreshToken", out _));
        Assert.Equal(entity.Id.ToString("D"), sent.Body.GetProperty("id").GetString());
    }

    [Fact]
    public async Task Refresh_UsesSingleAtomicRotateEndpoint()
    {
        var handler = new FakeWorkerHandler();
        var repository = CreateRepository(handler);
        var oldHash = new string('B', 64);

        var old = await repository.FindByHashAsync(oldHash);
        Assert.NotNull(old);
        old.RevokedAt = DateTimeOffset.UtcNow;

        var next = NewToken('C');
        await repository.AddAsync(next);
        await repository.SaveChangesAsync();

        Assert.Equal(2, handler.Records.Count);
        Assert.Equal("/v1/sessions/lookup", handler.Records[0].Path);
        Assert.Equal("/v1/sessions/rotate", handler.Records[1].Path);
        Assert.Equal(oldHash, handler.Records[1].Body.GetProperty("oldTokenHash").GetString());
        Assert.Equal(next.TokenHash, handler.Records[1].Body.GetProperty("newTokenHash").GetString());
        Assert.True(next.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Revoke_DoesNotIssueAnotherToken()
    {
        var handler = new FakeWorkerHandler();
        var repository = CreateRepository(handler);
        var old = await repository.FindByHashAsync(new string('D', 64));
        Assert.NotNull(old);

        old.RevokedAt = DateTimeOffset.UtcNow;
        await repository.SaveChangesAsync();

        Assert.Equal("/v1/sessions/revoke", handler.Records.Last().Path);
        Assert.Equal(2, handler.Records.Count);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, false)]
    [InlineData(HttpStatusCode.ServiceUnavailable, true)]
    public async Task Lookup_FailsClosedWhenSessionInvalidOrWorkerUnavailable(
        HttpStatusCode status,
        bool throws)
    {
        var handler = new FakeWorkerHandler { LookupStatus = status };
        var repository = CreateRepository(handler);
        if (throws)
        {
            await Assert.ThrowsAsync<HttpRequestException>(
                () => repository.FindByHashAsync(new string('F', 64)));
        }
        else
        {
            Assert.Null(await repository.FindByHashAsync(new string('F', 64)));
        }
    }

    private static CloudflareRefreshTokenRepository CreateRepository(FakeWorkerHandler handler)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://test.invalid/") };
        var options = Options.Create(new CloudflareSessionOptions
        {
            Enabled = true,
            ServiceBearer = Bearer,
            BaseUrl = "https://test.invalid/",
        });
        return new CloudflareRefreshTokenRepository(
            client, new StubUserRepository(), options);
    }

    private static RefreshToken NewToken(char character) => new()
    {
        Id = Guid.NewGuid(),
        UserId = UserId,
        TokenHash = new string(character, 64),
        CreatedAt = DateTimeOffset.UtcNow,
        ExpiresAt = DateTimeOffset.UtcNow.AddDays(14),
    };

    private sealed class StubUserRepository : IUserRepository
    {
        public Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
            => Task.FromResult(false);
        public Task<User?> FindByEmailAsync(string email, CancellationToken ct = default)
            => Task.FromResult<User?>(null);
        public Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<User?>(id == UserId ? new User { Id = id } : null);
        public Task AddAsync(User user, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class FakeWorkerHandler : HttpMessageHandler
    {
        public HttpStatusCode LookupStatus { get; init; } = HttpStatusCode.OK;
        public List<(string Path, string? Authorization, JsonElement Body)> Records { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadFromJsonAsync<JsonElement>(
                cancellationToken: cancellationToken);
            var path = request.RequestUri!.AbsolutePath;
            Records.Add((path, request.Headers.Authorization?.ToString(), body));

            if (path == "/v1/sessions/lookup")
            {
                if (LookupStatus != HttpStatusCode.OK)
                    return new HttpResponseMessage(LookupStatus);

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new
                    {
                        id = Guid.NewGuid(),
                        userId = UserId,
                        familyId = FamilyId,
                        expiresAt = DateTimeOffset.UtcNow.AddDays(14).ToUnixTimeSeconds(),
                    }),
                };
            }
            return new HttpResponseMessage(path == "/v1/sessions/issue"
                ? HttpStatusCode.Created : HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { success = true }),
            };
        }
    }
}
