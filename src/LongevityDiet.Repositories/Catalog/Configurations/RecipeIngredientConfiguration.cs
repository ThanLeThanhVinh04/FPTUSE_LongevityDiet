using LongevityDiet.Domain.Catalog.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LongevityDiet.Repositories.Catalog.Configurations;

public sealed class RecipeIngredientConfiguration : IEntityTypeConfiguration<RecipeIngredient>
{
    public void Configure(EntityTypeBuilder<RecipeIngredient> builder)
    {
        builder.ToTable("RecipeIngredients", "catalog");
        builder.HasKey(x => new { x.RecipeId, x.FoodId });
        builder.Property(x => x.Amount).HasPrecision(10, 2);
        builder.Property(x => x.Unit).HasMaxLength(40).IsRequired();
        builder.HasIndex(x => x.FoodId);
        builder.HasOne(x => x.Recipe)
            .WithMany(x => x.Ingredients)
            .HasForeignKey(x => x.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Food)
            .WithMany(x => x.RecipeIngredients)
            .HasForeignKey(x => x.FoodId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
