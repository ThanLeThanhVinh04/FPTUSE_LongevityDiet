using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LongevityDiet.Tracking;

public sealed class TrackingDesignTimeFactory : IDesignTimeDbContextFactory<TrackingDbContext>
{
    public TrackingDbContext CreateDbContext(string[] args)
    {
        // Only used by dotnet-ef to calculate migrations; never connects here.
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Tracking")
            ?? "Server=localhost;Database=LongevityTrackingDb;Trusted_Connection=True;TrustServerCertificate=True";
        var builder = new DbContextOptionsBuilder<TrackingDbContext>()
            .UseSqlServer(connection);
        return new TrackingDbContext(builder.Options);
    }
}
