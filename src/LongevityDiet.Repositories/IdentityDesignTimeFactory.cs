using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LongevityDiet.Repositories;

public sealed class IdentityDesignTimeFactory : IDesignTimeDbContextFactory<LongevityDietDbContext>
{
    public LongevityDietDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Server=localhost;Database=LongevityDietDb;Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<LongevityDietDbContext>()
            .UseSqlServer(connection).Options;
        return new LongevityDietDbContext(options);
    }
}
