using System.Globalization;
using System.Text;
using LongevityDiet.API.OpenApi;
using LongevityDiet.API.Recommendation;
using LongevityDiet.API.Security;
using LongevityDiet.Domain.Entities;
using LongevityDiet.Repositories;
using LongevityDiet.Repositories.Catalog.Repositories;
using LongevityDiet.Repositories.Catalog.Seed;
using LongevityDiet.Repositories.Repositories;
using LongevityDiet.Repositories.Seed;
using LongevityDiet.Services.Catalog;
using LongevityDiet.Services.Identity;
using LongevityDiet.Services.Fmd;
using LongevityDiet.Services.Planning;
using LongevityDiet.Services.Recommendation;
using LongevityDiet.Services.Tracking;
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
    builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
    builder.Services.AddScoped<IFoodRepository, FoodRepository>();
    builder.Services.AddScoped<IRecipeRepository, RecipeRepository>();
    builder.Services.AddScoped<IRuleCatalogRepository, RuleCatalogRepository>();
    builder.Services.AddScoped<IMealPlanRepository, MealPlanRepository>();
    builder.Services.AddScoped<IRecommendationRepository, RecommendationRepository>();
    builder.Services.AddScoped<IFmdRepository, FmdRepository>();
    builder.Services.AddScoped<IMealLogRepository, MealLogRepository>();
    builder.Services.AddScoped<IEatingWindowRepository, EatingWindowRepository>();
    builder.Services.AddScoped<IChallengeProgressRepository, ChallengeProgressRepository>();
    builder.Services.AddScoped<IPlanningInputProvider, PlanningCatalogSnapshotProvider>();
    builder.Services.AddScoped<ITrackingUserScheduleProvider, TrackingUserScheduleProvider>();
    builder.Services.AddSingleton<IRecommendationEngineClient, GrpcRecommendationEngineClient>();
    builder.Services.AddScoped<SafeExplanationRewriter>();
    builder.Services.AddScoped<AuthenticationService>();
    builder.Services.AddScoped<ProfileService>();
    builder.Services.AddScoped<FoodCatalogService>();
    builder.Services.AddScoped<RecipeCatalogService>();
    builder.Services.AddScoped<RuleCatalogService>();
    builder.Services.AddScoped<MealPlanningService>();
    builder.Services.AddScoped<RecommendationService>();
    builder.Services.AddScoped<FmdService>();
    builder.Services.AddScoped<MealTrackingService>();
    builder.Services.AddScoped<ChallengeProgressService>();
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
        await CatalogSeed.SeedAsync(dbContext);
        await ChallengeSeed.SeedAsync(dbContext);
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
