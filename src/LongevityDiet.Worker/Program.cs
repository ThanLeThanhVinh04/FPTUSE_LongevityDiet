using System.Globalization;
using LongevityDiet.Repositories;
using LongevityDiet.Repositories.Repositories;
using LongevityDiet.Services.Tracking;
using LongevityDiet.Worker;
using Microsoft.EntityFrameworkCore;
using Serilog;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);

Log.Logger = new LoggerConfiguration().WriteTo.Console(formatProvider: CultureInfo.InvariantCulture).CreateLogger();
builder.Services.AddSerilog();

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is required.");
var redisConnectionString = builder.Configuration["Redis:ConnectionString"]
    ?? throw new InvalidOperationException("Redis:ConnectionString is required.");

builder.Services.AddDbContext<LongevityDietDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));
builder.Services.AddScoped<IWeeklyReportWorkerRepository, WeeklyReportWorkerRepository>();
builder.Services.AddScoped<IChallengeProgressRepository, ChallengeProgressRepository>();
builder.Services.AddScoped<ITrackingUserScheduleProvider, TrackingUserScheduleProvider>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<WeeklyReportService>();
builder.Services.AddScoped<WorkerReliabilityService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnectionString));
builder.Services.AddHostedService<WeeklyReportWorker>();
builder.Services.AddHostedService<RedisWeeklyReportStreamWorker>();

var host = builder.Build();
await host.RunAsync();
