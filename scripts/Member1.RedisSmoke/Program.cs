using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LongevityDiet.API.Reminders;
using LongevityDiet.IntegrationTests;
using LongevityDiet.Worker.Reminders;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

var redisAddress = args.Length > 0 ? args[0] : "127.0.0.1:16389";
Console.WriteLine("Member 1 Redis+SQLite E2E validation (temporary broker, no SQL Server).");

using var factory = new IntegrationTestWebApplicationFactory();
await factory.ResetDatabaseAsync();
using var client = factory.CreateHttpsClient();

var registration = await client.PostAsJsonAsync("/api/v1/auth/register", new
{
    email = $"redis-smoke-{Guid.NewGuid():N}@example.test",
    password = "StrongPass123!",
    displayName = "Redis Smoke User",
});
Require(registration.StatusCode == HttpStatusCode.Created,
    $"Registration returned {(int)registration.StatusCode}");
using var session = JsonDocument.Parse(await registration.Content.ReadAsStringAsync());
var token = session.RootElement.GetProperty("accessToken").GetString();
client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

await using var workerConnection = new SqliteConnection("Data Source=:memory:");
await workerConnection.OpenAsync();
var workerServices = new ServiceCollection()
    .AddSingleton<TimeProvider>(TimeProvider.System)
    .AddDbContext<ReminderWorkerDbContext>(options => options.UseSqlite(workerConnection))
    .AddScoped<ReminderWorkerEngine>();
using var workerProvider = workerServices.BuildServiceProvider(
    new ServiceProviderOptions { ValidateScopes = true });
using (var scope = workerProvider.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<ReminderWorkerDbContext>()
        .Database.EnsureCreatedAsync();
}
var redisConfig = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Redis:ConnectionString"] = redisAddress,
    }).Build();

var worker = new ReminderWorkerHostedService(
    workerProvider.GetRequiredService<IServiceScopeFactory>(),
    redisConfig, TimeProvider.System, NullLogger<ReminderWorkerHostedService>.Instance);
var bridge = new IdentityRedisBridge(
    factory.Services.GetRequiredService<IServiceScopeFactory>(),
    redisConfig, NullLogger<IdentityRedisBridge>.Instance, TimeProvider.System);

try
{
    await worker.StartAsync(CancellationToken.None);
    await bridge.StartAsync(CancellationToken.None);

    var prefResponse = await client.PutAsJsonAsync("/api/v1/reminders/preferences", new
    {
        optedIn = true, mealLoggingEnabled = false, activityEnabled = true,
        challengeEnabled = false, weeklySummaryEnabled = false,
        mealTime = "20:00:00", activityTime = "18:30:00",
        challengeTime = "21:00:00", weeklySummaryTime = "09:00:00",
        quietStart = "22:00:00", quietEnd = "07:00:00",
    });
    Require(prefResponse.StatusCode == HttpStatusCode.OK,
        $"Preference update returned {(int)prefResponse.StatusCode}");

    await UntilAsync(async () =>
    {
        using var scope = workerProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReminderWorkerDbContext>();
        return await db.Schedules.CountAsync() == 4;
    }, "Preference event reached Worker SQL");

    using (var scope = workerProvider.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ReminderWorkerDbContext>();
        var due = await db.Schedules.SingleAsync(s => s.Category == LongevityDiet.Domain.Entities.ReminderCategory.Activity);
        due.NextDueUtc = DateTime.UtcNow.AddSeconds(-1);
        await db.SaveChangesAsync();
    }

    await UntilAsync(async () =>
    {
        var response = await client.GetAsync("/api/v1/notifications");
        if (!response.IsSuccessStatusCode) return false;
        using var data = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return data.RootElement.GetProperty("items").GetArrayLength() == 1;
    }, "Worker result reached Identity Notification Inbox");

    using (var scope = workerProvider.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ReminderWorkerDbContext>();
        Require(await db.DispatchRecords.CountAsync() == 1, "Dispatch count was not 1");
        Require(await db.Outbox.CountAsync() == 1, "Worker outbox count was not 1");
        Require(await db.ProcessedEvents.CountAsync() == 1, "Worker inbox count was not 1");
    }

    var inboxResponse = await client.GetAsync("/api/v1/notifications");
    using var inbox = JsonDocument.Parse(await inboxResponse.Content.ReadAsStringAsync());
    var notification = inbox.RootElement.GetProperty("items")[0];
    var id = notification.GetProperty("id").GetGuid();
    Require(notification.GetProperty("deepLink").GetString() == "/activity",
        "Notification deep link mismatch");

    using var mark = await client.PostAsync($"/api/v1/notifications/{id}/read", null);
    Require(mark.StatusCode == HttpStatusCode.NoContent,
        "Mark-read API did not return 204");
    using var count = JsonDocument.Parse(await client.GetStringAsync("/api/v1/notifications/unread-count"));
    Require(count.RootElement.GetProperty("count").GetInt32() == 0,
        "Read-state did not persist");

    Console.WriteLine("PASS: Auth -> Identity Outbox -> Redis -> WorkerDb -> Worker Outbox");
    Console.WriteLine("PASS: Redis -> Identity Inbox -> Notification API -> Mark Read");
    Console.WriteLine("PASS: One dispatch, one deduplicated user notification");
    Console.WriteLine("NOTE: This is SQLite + real Redis; Microsoft SQL Server still requires separate acceptance.");
}
finally
{
    await bridge.StopAsync(CancellationToken.None);
    await worker.StopAsync(CancellationToken.None);
    bridge.Dispose();
    worker.Dispose();
}

static async Task UntilAsync(Func<Task<bool>> predicate, string operation)
{
    var timer = Stopwatch.StartNew();
    while (timer.Elapsed < TimeSpan.FromSeconds(30))
    {
        if (await predicate())
        {
            Console.WriteLine($"PASS: {operation} after {timer.Elapsed.TotalSeconds:F1}s");
            return;
        }
        await Task.Delay(250);
    }
    throw new TimeoutException($"TIMEOUT: {operation}");
}

static void Require(bool result, string message)
{
    if (!result) throw new InvalidOperationException(message);
}
