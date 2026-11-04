using System.Globalization;
using System.Text;
using LongevityDiet.API.Cloudflare;
using LongevityDiet.API.OpenApi;
using LongevityDiet.API.Security;
using LongevityDiet.Domain.Entities;
using LongevityDiet.Repositories;
using LongevityDiet.Repositories.Repositories;
using LongevityDiet.Services.Identity;
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
    builder.Services.AddProblemDetails();
    builder.Services.AddHealthChecks();
    builder.Services.AddHttpContextAccessor();

    builder.Services.AddDbContext<LongevityDietDbContext>(options =>
        options.UseSqlServer(
            connectionString,
            sql => sql.EnableRetryOnFailure()));

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
    }

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
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapHealthChecks("/health");
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
