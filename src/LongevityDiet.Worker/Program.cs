using LongevityDiet.Worker.Reminders;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);
var sqlConnection = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is required for the Worker.");
var workerConnection = builder.Configuration.GetConnectionString("Worker");
if (string.IsNullOrWhiteSpace(workerConnection))
{
    var connection = new SqlConnectionStringBuilder(sqlConnection)
    {
        InitialCatalog = "LongevityWorkerDb",
    };
    workerConnection = connection.ConnectionString;
}

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<ReminderWorkerDbContext>(options =>
    options.UseSqlServer(workerConnection)); // manual business transactions; host retries transient failures
builder.Services.AddScoped<ReminderWorkerEngine>();
builder.Services.AddHostedService<ReminderWorkerHostedService>();

using var host = builder.Build();
if (host.Services.GetRequiredService<IHostEnvironment>().IsDevelopment())
{
    await using var scope = host.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<ReminderWorkerDbContext>()
        .Database.MigrateAsync();
}
await host.RunAsync();
