using System.Globalization;
using System.Diagnostics;
using System.Threading.RateLimiting;
using System.Text;
using LongevityDiet.API.Cloudflare;
using LongevityDiet.API.OpenApi;
using LongevityDiet.API.Security;
using LongevityDiet.API.Reminders;
using Microsoft.AspNetCore.RateLimiting;
using Serilog.Context;
using LongevityDiet.Domain.Entities;
using LongevityDiet.Repositories;
using LongevityDiet.Repositories.Repositories;
using LongevityDiet.Services.Identity;
using LongevityDiet.Services.Reminders;
using LongevityDiet.Tracking;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    var connectionString = builder.Configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("ConnectionStrings:Default is required.");

    var jwtOptions = builder.Configuration
        .GetSection(JwtOptions.SectionName)
        .Get<JwtOptions>()
        ?? throw new InvalidOperationException("Jwt configuration is required.");

    if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey) || jwtOptions.SigningKey.Length < 32)
    {
        throw new InvalidOperationException("Jwt:SigningKey must contain at least 32 characters.");
    }

    builder.Services.AddControllers();
    builder.Services.AddOpenApi(options =>
    {
        options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
        options.AddOperationTransformer<AuthorizeOperationTransformer>();
    });
    var readinessTags = new[] { "ready" };
    builder.Services.AddProblemDetails(options =>
    {
        options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    });
    builder.Services.AddHealthChecks()
        .AddCheck<LongevityDiet.API.Security.ReadinessCheck>("dependencies",
            tags: readinessTags);
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy("auth", context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "local",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 40, QueueLimit = 0,
                    Window = TimeSpan.FromMinutes(1),
                    AutoReplenishment = true,
                }));
        options.AddPolicy("member-api", context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ??
                    context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 150, QueueLimit = 0,
                    Window = TimeSpan.FromMinutes(1),
                    AutoReplenishment = true,
                }));
        options.OnRejected = async (context, cancellationToken) =>
        {
            var response = context.HttpContext.Response;
            response.StatusCode = StatusCodes.Status429TooManyRequests;
            response.Headers.RetryAfter = "60";
            response.ContentType = "application/problem+json";
            await response.WriteAsync(
                "{\"type\":\"about:blank\",\"title\":\"rate_limited\",\"status\":429,\"detail\":\"Request rate limit exceeded. Please retry later.\"}",
                cancellationToken);
        };
    });
    builder.Services.AddHttpContextAccessor();

    builder.Services.AddDbContext<LongevityDietDbContext>(options =>
        options.UseSqlServer(
            connectionString,
            sql => sql.EnableRetryOnFailure()));

    // Transitional single-host adapter. Tracking gets an independent DbContext
    // and separate SQL catalog, never the Identity DbContext.
    var trackingConnection = builder.Configuration.GetConnectionString("Tracking");
    if (string.IsNullOrWhiteSpace(trackingConnection))
    {
        var connection = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = "LongevityTrackingDb",
        };
        trackingConnection = connection.ConnectionString;
    }

    builder.Services.AddDbContext<TrackingDbContext>(options =>
        options.UseSqlServer(trackingConnection, sql => sql.EnableRetryOnFailure()));
    builder.Services.AddScoped<ITrackingUserContext, IdentityTrackingUserContext>();
    builder.Services.AddScoped<TrackingService>();

    builder.Services.AddScoped<IUserRepository, UserRepository>();
    // Default SQL persistence is intentionally unchanged until Cloudflare D1
    // has been provisioned, migrated, authenticated, and E2E verified.
    var cloudflareSessions = builder.Configuration
        .GetSection(CloudflareSessionOptions.SectionName)
        .Get<CloudflareSessionOptions>() ?? new CloudflareSessionOptions();

    builder.Services.Configure<CloudflareSessionOptions>(
        builder.Configuration.GetSection(CloudflareSessionOptions.SectionName));

    if (cloudflareSessions.Enabled)
    {
        if (!Uri.TryCreate(cloudflareSessions.BaseUrl, UriKind.Absolute, out var remote) ||
            remote.Scheme != Uri.UriSchemeHttps ||
            remote.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(remote.UserInfo) ||
            !string.IsNullOrWhiteSpace(remote.Query) ||
            !string.IsNullOrWhiteSpace(remote.Fragment) ||
            string.IsNullOrWhiteSpace(cloudflareSessions.ServiceBearer) ||
            cloudflareSessions.ServiceBearer.Length < 32)
        {
            throw new InvalidOperationException(
                "CloudflareSessions requires a root HTTPS BaseUrl and ServiceBearer secret (at least 32 characters).");
        }

        builder.Services.AddHttpClient<IRefreshTokenRepository, CloudflareRefreshTokenRepository>(
            client =>
            {
                client.BaseAddress = remote;
                client.Timeout = TimeSpan.FromSeconds(5);
            });
    }
    else
    {
        builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
    }
    builder.Services.AddScoped<AuthenticationService>();
    builder.Services.AddScoped<ProfileService>();
    builder.Services.AddScoped<ReminderService>();
    builder.Services.AddTransient<LongevityDiet.API.Security.ReadinessCheck>();
    if (!builder.Environment.IsEnvironment("Testing"))
        builder.Services.AddHostedService<IdentityRedisBridge>();
    builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
    builder.Services.AddScoped<IAccessTokenProvider, JwtAccessTokenProvider>();
    builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
    builder.Services.AddSingleton(TimeProvider.System);

    builder.Services.Configure<JwtOptions>(
        builder.Configuration.GetSection(JwtOptions.SectionName));
    builder.Services.Configure<AuthenticationOptions>(
        builder.Configuration.GetSection(AuthenticationOptions.SectionName));

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtOptions.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
                NameClaimType = System.Security.Claims.ClaimTypes.Name,
                RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            };
        });

    builder.Services.AddAuthorization(AuthorizationPolicies.Configure);

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        await using var scope = app.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LongevityDietDbContext>();
        await dbContext.Database.MigrateAsync();
        var trackingDb = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();
        await trackingDb.Database.MigrateAsync();
    }

    app.Use(async (httpContext, next) =>
    {
        var incoming = httpContext.Request.Headers["X-Correlation-ID"].ToString();
        var safe = incoming.Length is > 0 and <= 64 && incoming.All(ch =>
            ch is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_');
        var correlation = safe ? incoming : Guid.NewGuid().ToString("N");
        httpContext.TraceIdentifier = correlation;
        httpContext.Response.Headers["X-Correlation-ID"] = correlation;
        Activity.Current?.SetTag("correlation.id", correlation);
        using (LogContext.PushProperty("CorrelationId", correlation))
        {
            await next(httpContext);
        }
    });
    app.UseSerilogRequestLogging();
    app.UseExceptionHandler();

    if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
    {
        app.MapOpenApi();
    }

    if (app.Environment.IsDevelopment())
    {
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/openapi/v1.json", "Longevity Diet API v1");
            options.RoutePrefix = "swagger";
            options.DocumentTitle = "Longevity Diet API";
        });
    }

    app.UseHttpsRedirection();
    app.UseRouting();
    app.UseAuthentication();
    app.UseRateLimiter();
    app.UseAuthorization();

    app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => false, // liveness: process responds, no dependency required
    });
    app.MapHealthChecks("/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"), // actual dependencies
    });
    app.MapControllers();

    app.Run();
}
catch (HostAbortedException)
{
    // Expected when EF Core design-time tooling stops the host after resolving services.
}
catch (Exception ex)
{
    Log.Fatal(ex, "LongevityDiet.API terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
