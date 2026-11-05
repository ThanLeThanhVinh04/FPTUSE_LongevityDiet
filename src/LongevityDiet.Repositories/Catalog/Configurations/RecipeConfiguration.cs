using LongevityDiet.Domain.Catalog.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LongevityDiet.Repositories.Catalog.Configurations;

public sealed class RecipeConfiguration : IEntityTypeConfiguration<Recipe>
{
    public void Configure(EntityTypeBuilder<Recipe> builder)
    {
        builder.ToTable("Recipes", "catalog");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(180).IsRequired();
        builder.Property(x => x.NormalizedName).HasMaxLength(180).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Cuisine).HasMaxLength(100);
        builder.Property(x => x.Tags).HasMaxLength(500);
        builder.Property(x => x.CaloriesPerServing).HasPrecision(10, 2);
        builder.Property(x => x.ProteinGPerServing).HasPrecision(10, 2);
        builder.Property(x => x.CarbohydrateGPerServing).HasPrecision(10, 2);
        builder.Property(x => x.FatGPerServing).HasPrecision(10, 2);
        builder.Property(x => x.SaturatedFatGPerServing).HasPrecision(10, 2);
        builder.Property(x => x.SugarGPerServing).HasPrecision(10, 2);
        builder.Property(x => x.FiberGPerServing).HasPrecision(10, 2);
        builder.HasIndex(x => x.NormalizedName);
        builder.HasIndex(x => new { x.IsActive, x.Category, x.Cuisine });
    }
}
