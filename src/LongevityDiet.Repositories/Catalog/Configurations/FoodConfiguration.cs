using LongevityDiet.Domain.Catalog.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LongevityDiet.Repositories.Catalog.Configurations;

public sealed class FoodConfiguration : IEntityTypeConfiguration<Food>
{
    public void Configure(EntityTypeBuilder<Food> builder)
    {
        builder.ToTable("Foods", "catalog");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(180).IsRequired();
        builder.Property(x => x.NormalizedName).HasMaxLength(180).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Tags).HasMaxLength(500);
        builder.Property(x => x.Cuisine).HasMaxLength(100);
        builder.Property(x => x.ServingUnit).HasMaxLength(40).IsRequired();
        builder.Property(x => x.DefaultServingAmount).HasPrecision(10, 2);
        builder.Property(x => x.Calories).HasPrecision(10, 2);
        builder.Property(x => x.ProteinG).HasPrecision(10, 2);
        builder.Property(x => x.CarbohydrateG).HasPrecision(10, 2);
        builder.Property(x => x.FatG).HasPrecision(10, 2);
        builder.Property(x => x.SaturatedFatG).HasPrecision(10, 2);
        builder.Property(x => x.SugarG).HasPrecision(10, 2);
        builder.Property(x => x.FiberG).HasPrecision(10, 2);
        builder.HasIndex(x => x.NormalizedName);
        builder.HasIndex(x => new { x.IsActive, x.Category });
    }
}
