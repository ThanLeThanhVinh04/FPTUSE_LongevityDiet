using LongevityDiet.Domain.Catalog.Entities;
using LongevityDiet.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace LongevityDiet.UnitTests.Catalog;

public sealed class CatalogModelTests
{
    private static LongevityDietDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<LongevityDietDbContext>()
            .UseSqlServer("Server=(local);Database=LongevityDietModelTests;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        return new LongevityDietDbContext(options);
    }

    [Theory]
    [InlineData(typeof(Food), "Foods")]
    [InlineData(typeof(Recipe), "Recipes")]
    [InlineData(typeof(RecipeIngredient), "RecipeIngredients")]
    [InlineData(typeof(Allergen), "Allergens")]
    [InlineData(typeof(RecipeAllergen), "RecipeAllergens")]
    [InlineData(typeof(DietRule), "DietRules")]
    [InlineData(typeof(RuleVersion), "RuleVersions")]
    [InlineData(typeof(RuleSetVersion), "RuleSetVersions")]
    public void CatalogEntities_UseDedicatedCatalogSchema(Type entityType, string tableName)
    {
        using var context = CreateContext();

        var metadata = context.Model.FindEntityType(entityType);

        Assert.NotNull(metadata);
        Assert.Equal("catalog", metadata!.GetSchema());
        Assert.Equal(tableName, metadata.GetTableName());
    }

    [Fact]
    public void CatalogEntities_DoNotReferenceIdentityEntities()
    {
        using var context = CreateContext();
        var catalogTypes = new[]
        {
            typeof(Food),
            typeof(Recipe),
            typeof(RecipeIngredient),
            typeof(Allergen),
            typeof(RecipeAllergen),
            typeof(DietRule),
            typeof(RuleVersion),
            typeof(RuleSetVersion)
        };

        foreach (var catalogType in catalogTypes)
        {
            var metadata = context.Model.FindEntityType(catalogType);
            Assert.NotNull(metadata);

            Assert.DoesNotContain(
                metadata!.GetForeignKeys(),
                foreignKey => foreignKey.PrincipalEntityType.ClrType.Namespace == "LongevityDiet.Domain.Entities");
        }
    }

    [Fact]
    public void RuleVersion_HasUniqueRuleAndVersionIndex()
    {
        using var context = CreateContext();
        var metadata = context.Model.FindEntityType(typeof(RuleVersion));
        Assert.NotNull(metadata);

        var index = metadata!.GetIndexes().SingleOrDefault(
            candidate => candidate.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(RuleVersion.DietRuleId), nameof(RuleVersion.VersionNumber) }));

        Assert.NotNull(index);
        Assert.True(index!.IsUnique);
    }
}
