using LongevityDiet.Domain.Catalog.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LongevityDiet.Repositories.Catalog.Configurations;

public sealed class RecipeAllergenConfiguration : IEntityTypeConfiguration<RecipeAllergen>
{
    public void Configure(EntityTypeBuilder<RecipeAllergen> builder)
    {
        builder.ToTable("RecipeAllergens", "catalog");
        builder.HasKey(x => new { x.RecipeId, x.AllergenId });
        builder.HasIndex(x => x.AllergenId);
        builder.HasOne(x => x.Recipe)
            .WithMany(x => x.Allergens)
            .HasForeignKey(x => x.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Allergen)
            .WithMany(x => x.Recipes)
            .HasForeignKey(x => x.AllergenId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
