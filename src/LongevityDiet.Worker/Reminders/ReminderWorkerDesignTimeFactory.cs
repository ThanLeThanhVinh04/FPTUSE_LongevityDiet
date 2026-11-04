using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LongevityDiet.Worker.Reminders;

public sealed class ReminderWorkerDesignTimeFactory : IDesignTimeDbContextFactory<ReminderWorkerDbContext>
{
    public ReminderWorkerDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<ReminderWorkerDbContext>()
            .UseSqlServer(Environment.GetEnvironmentVariable("ConnectionStrings__Worker")
                ?? "Server=localhost;Database=LongevityWorkerDb;Trusted_Connection=True;TrustServerCertificate=True")
            .Options);
}
