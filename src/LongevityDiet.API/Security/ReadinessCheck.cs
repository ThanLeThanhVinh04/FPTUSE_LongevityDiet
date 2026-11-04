using LongevityDiet.Repositories;
using LongevityDiet.Tracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace LongevityDiet.API.Security;

/// <summary>
/// Readiness is different from liveness. Do not report ready until both service
/// databases and the event broker can be reached in production.
/// </summary>
public sealed class ReadinessCheck(
    LongevityDietDbContext identity,
    TrackingDbContext tracking,
    IConfiguration configuration,
    IHostEnvironment environment) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await identity.Database.CanConnectAsync(cancellationToken))
                return HealthCheckResult.Unhealthy("Identity database is unavailable.");
            if (!await tracking.Database.CanConnectAsync(cancellationToken))
                return HealthCheckResult.Unhealthy("Tracking database is unavailable.");
            // Verify expected tables, not merely that a socket to SQL can open.
            await identity.Users.AsNoTracking().Select(u => u.Id)
                .Take(1).ToArrayAsync(cancellationToken);
            await tracking.ScoreConfigurations.AsNoTracking().Select(v => v.Version)
                .Take(1).ToArrayAsync(cancellationToken);

            var redis = configuration["Redis:ConnectionString"];
            if (string.IsNullOrWhiteSpace(redis))
            {
                return environment.IsEnvironment("Testing")
                    ? HealthCheckResult.Healthy("Database connections verified in test environment.")
                    : HealthCheckResult.Unhealthy("Redis configuration is missing.");
            }

            var options = ConfigurationOptions.Parse(redis);
            options.AbortOnConnectFail = true;
            options.ConnectTimeout = 1500;
            options.SyncTimeout = 1500;
            options.ConnectRetry = 0;

            using var mux = await ConnectionMultiplexer.ConnectAsync(options);
            if (!await mux.GetDatabase().PingAsync().WaitAsync(
                TimeSpan.FromSeconds(2), cancellationToken).ContinueWith(
                task => task.IsCompletedSuccessfully, cancellationToken))
                return HealthCheckResult.Unhealthy("Redis ping was unsuccessful.");

            return HealthCheckResult.Healthy("Identity, Tracking SQL and Redis are available.");
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // Don't leak DB hosts, secrets, or exception messages to health endpoint.
            return HealthCheckResult.Unhealthy("One or more required dependencies are unavailable.");
        }
    }
}
