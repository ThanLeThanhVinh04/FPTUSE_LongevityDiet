using LongevityDiet.Repositories;
using LongevityDiet.Repositories.Catalog.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LongevityDiet.IntegrationTests;

public sealed class CatalogSeedTests : IClassFixture<IntegrationTestWebApplicationFactory>
{
    private readonly IntegrationTestWebApplicationFactory _factory;

    public CatalogSeedTests(IntegrationTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SeedAsync_IsDeterministicAndIdempotent()
    {
        await _factory.ResetDatabaseAsync();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LongevityDietDbContext>();

        await CatalogSeed.SeedAsync(dbContext);
        await CatalogSeed.SeedAsync(dbContext);

        Assert.Equal(4, await dbContext.Foods.CountAsync());
        Assert.Equal(2, await dbContext.Allergens.CountAsync());
        Assert.Equal(2, await dbContext.Recipes.CountAsync());
        Assert.Equal(6, await dbContext.RecipeIngredients.CountAsync());
        Assert.Single(await dbContext.RecipeAllergens.ToListAsync());
        Assert.Equal(2, await dbContext.DietRules.CountAsync());
        Assert.Equal(2, await dbContext.RuleVersions.CountAsync());
        Assert.Single(await dbContext.RuleSetVersions.ToListAsync());

        var activeSet = await dbContext.RuleSetVersions
            .Include(x => x.RuleVersions)
            .SingleAsync(x => x.IsActive);

        Assert.Equal(2, activeSet.RuleVersions.Count);
    }
}
