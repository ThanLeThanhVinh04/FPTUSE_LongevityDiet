using LongevityDiet.Domain.Entities;
using LongevityDiet.Repositories;
using LongevityDiet.Tracking;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LongevityDiet.IntegrationTests;

public sealed class IntegrationTestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly SqliteConnection _trackingConnection = new("Data Source=:memory:");

    public IntegrationTestWebApplicationFactory()
    {
        _connection.Open();
        _trackingConnection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseContentRoot(ResolveApiContentRoot());

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Data Source=integration-tests",
                ["Jwt:Issuer"] = "LongevityDiet.API",
                ["Jwt:Audience"] = "LongevityDiet.Web",
                ["Jwt:SigningKey"] = "IntegrationTestsOnly_SigningKey_AtLeast_32_Characters_Long",
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Authentication:RefreshTokenDays"] = "14",
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDbContextOptionsConfiguration<LongevityDietDbContext>>();
            services.RemoveAll<DbContextOptions<LongevityDietDbContext>>();
            services.RemoveAll<LongevityDietDbContext>();

            services.AddDbContext<LongevityDietDbContext>(options =>
                options.UseSqlite(_connection));

            services.RemoveAll<IDbContextOptionsConfiguration<TrackingDbContext>>();
            services.RemoveAll<DbContextOptions<TrackingDbContext>>();
            services.RemoveAll<TrackingDbContext>();
            services.AddDbContext<TrackingDbContext>(options =>
                options.UseSqlite(_trackingConnection));
        });
    }

    private static string ResolveApiContentRoot()
    {
        // Works from dotnet test, console smoke runner and CI output folders.
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "src", "LongevityDiet.API");
            if (File.Exists(Path.Combine(candidate, "LongevityDiet.API.csproj")))
                return candidate;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate LongevityDiet.API source root.");
    }

    public HttpClient CreateHttpsClient() =>
        CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LongevityDietDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var tracking = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();
        await tracking.Database.EnsureDeletedAsync();
        await tracking.Database.EnsureCreatedAsync();
    }

    public async Task DisableUserAsync(string email)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LongevityDietDbContext>();

        var normalizedEmail = email.Trim().ToUpperInvariant();
        var user = await dbContext.Users
            .SingleAsync(x => x.NormalizedEmail == normalizedEmail);

        user.Status = AccountStatus.Disabled;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();
            _trackingConnection.Dispose();
        }
    }
}
