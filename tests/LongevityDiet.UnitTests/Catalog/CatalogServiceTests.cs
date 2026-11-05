using LongevityDiet.Domain.Catalog;
using LongevityDiet.Domain.Catalog.Entities;
using LongevityDiet.Repositories.Catalog.Queries;
using LongevityDiet.Repositories.Catalog.Repositories;
using LongevityDiet.Services.Catalog;

namespace LongevityDiet.UnitTests.Catalog;

public sealed class CatalogServiceTests
{
    [Fact]
    public async Task FoodQuery_RejectsPageSizeAboveMaximum()
    {
        var repository = new FakeFoodRepository();
        var service = new FoodCatalogService(repository, TimeProvider.System);

        var result = await service.QueryAsync(new FoodQueryOptions(PageSize: 101));

        Assert.False(result.Succeeded);
        Assert.Equal("invalid_page_size", result.ErrorCode);
        Assert.Null(repository.LastQuery);
    }

    [Fact]
    public async Task FoodQuery_MapsAllowListedSortAndActiveOnly()
    {
        var repository = new FakeFoodRepository();
        var service = new FoodCatalogService(repository, TimeProvider.System);

        var result = await service.QueryAsync(
            new FoodQueryOptions(SortBy: "calories", SortDirection: "desc"));

        Assert.True(result.Succeeded);
        Assert.NotNull(repository.LastQuery);
        Assert.Equal(FoodSortField.Calories, repository.LastQuery!.SortField);
        Assert.Equal(SortDirection.Desc, repository.LastQuery.SortDirection);
        Assert.True(repository.LastQuery.ActiveOnly);
    }

    [Fact]
    public async Task FoodDeactivate_PerformsSoftDeactivate()
    {
        var food = new Food
        {
            Name = "Lentils",
            NormalizedName = "LENTILS",
            Category = "Legume",
        };
        var repository = new FakeFoodRepository(food);
        var service = new FoodCatalogService(repository, TimeProvider.System);

        var result = await service.DeactivateAsync(food.Id);

        Assert.True(result.Succeeded);
        Assert.False(food.IsActive);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task RecipeCreate_MapsIngredientsAndAllergens()
    {
        var food = new Food { Name = "Tofu", NormalizedName = "TOFU", Category = "Protein" };
        var allergen = new Allergen { Code = "SOY", Name = "Soy" };
        var repository = new FakeRecipeRepository([food], [allergen]);
        var service = new RecipeCatalogService(repository, TimeProvider.System);

        var command = CreateRecipeCommand(
            [new RecipeIngredientInput(food.Id, 100, "g", false)],
            [allergen.Id]);

        var result = await service.CreateAsync(command);

        Assert.True(result.Succeeded);
        Assert.NotNull(repository.StoredRecipe);
        Assert.Single(repository.StoredRecipe!.Ingredients);
        Assert.Single(repository.StoredRecipe.Allergens);
        Assert.Equal(allergen.Id, repository.StoredRecipe.Allergens.Single().AllergenId);
    }

    [Fact]
    public void RecipeAllergenExclusionPolicy_DetectsConfiguredExclusion()
    {
        var soy = Guid.NewGuid();
        var fish = Guid.NewGuid();
        var sesame = Guid.NewGuid();

        var excluded = RecipeAllergenExclusionPolicy.IsExcluded(
            [soy, fish],
            [sesame, fish]);

        var allowed = RecipeAllergenExclusionPolicy.IsExcluded(
            [soy],
            [sesame]);

        Assert.True(excluded);
        Assert.False(allowed);
    }

    [Fact]
    public async Task RuleUpdate_PublishedVersionIsImmutable()
    {
        var version = new RuleVersion
        {
            DietRuleId = Guid.NewGuid(),
            VersionNumber = 1,
            State = RuleVersionState.Published,
            ParametersJson = "{}",
            SourceUrl = "https://example.com/source",
            Citation = "Published source",
        };
        var repository = new FakeRuleRepository(version: version);
        var service = new RuleCatalogService(repository, TimeProvider.System);

        var result = await service.UpdateVersionAsync(
            version.Id,
            new UpdateRuleVersionCommand(
                "{\"changed\":true}",
                "https://example.com/new",
                SourceType.OfficialGuidance,
                "New citation",
                EvidenceLevel.High,
                null));

        Assert.False(result.Succeeded);
        Assert.Equal("published_rule_immutable", result.ErrorCode);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task RulePublish_RejectsMissingProvenance()
    {
        var version = new RuleVersion
        {
            DietRuleId = Guid.NewGuid(),
            VersionNumber = 1,
            State = RuleVersionState.Draft,
            ParametersJson = "{}",
            SourceUrl = string.Empty,
            Citation = string.Empty,
        };
        var repository = new FakeRuleRepository(version: version);
        var service = new RuleCatalogService(repository, TimeProvider.System);

        var result = await service.PublishVersionAsync(version.Id);

        Assert.False(result.Succeeded);
        Assert.Equal("missing_provenance", result.ErrorCode);
        Assert.Equal(RuleVersionState.Draft, version.State);
        Assert.Equal(0, repository.SaveCount);
    }

    private static SaveRecipeCommand CreateRecipeCommand(
        IReadOnlyList<RecipeIngredientInput> ingredients,
        IReadOnlyList<Guid> allergenIds) =>
        new(
            "Tofu Bowl",
            "Team-authored recipe",
            "Main",
            "Vietnamese",
            "plant-forward",
            2,
            20,
            420,
            22,
            55,
            12,
            2,
            5,
            8,
            true,
            true,
            true,
            false,
            ingredients,
            allergenIds);

    private sealed class FakeFoodRepository(params Food[] foods) : IFoodRepository
    {
        private readonly List<Food> _foods = [.. foods];

        public FoodQuery? LastQuery { get; private set; }
        public int SaveCount { get; private set; }

        public Task<RepositoryPage<Food>> QueryAsync(
            FoodQuery query,
            CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            return Task.FromResult(new RepositoryPage<Food>(_foods, _foods.Count));
        }

        public Task<Food?> FindByIdAsync(
            Guid id,
            bool includeInactive,
            bool trackChanges = false,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_foods.FirstOrDefault(x => x.Id == id && (includeInactive || x.IsActive)));

        public Task AddAsync(Food food, CancellationToken cancellationToken = default)
        {
            _foods.Add(food);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRecipeRepository(
        IReadOnlyList<Food> foods,
        IReadOnlyList<Allergen> allergens) : IRecipeRepository
    {
        public Recipe? StoredRecipe { get; private set; }

        public Task<RepositoryPage<Recipe>> QueryAsync(
            RecipeQuery query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new RepositoryPage<Recipe>([], 0));

        public Task<Recipe?> FindByIdAsync(
            Guid id,
            bool includeInactive,
            bool trackChanges = false,
            CancellationToken cancellationToken = default)
        {
            if (StoredRecipe?.Id != id)
            {
                return Task.FromResult<Recipe?>(null);
            }

            foreach (var ingredient in StoredRecipe.Ingredients)
            {
                ingredient.Food = foods.Single(x => x.Id == ingredient.FoodId);
            }

            foreach (var recipeAllergen in StoredRecipe.Allergens)
            {
                recipeAllergen.Allergen = allergens.Single(x => x.Id == recipeAllergen.AllergenId);
            }

            return Task.FromResult<Recipe?>(StoredRecipe);
        }

        public Task<IReadOnlyList<Food>> FindFoodsAsync(
            IReadOnlyCollection<Guid> ids,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Food>>(foods.Where(x => ids.Contains(x.Id)).ToList());

        public Task<IReadOnlyList<Allergen>> GetActiveAllergensAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(allergens.Where(x => x.IsActive).ToList() as IReadOnlyList<Allergen>);

        public Task<IReadOnlyList<Allergen>> FindAllergensAsync(
            IReadOnlyCollection<Guid> ids,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Allergen>>(allergens.Where(x => ids.Contains(x.Id)).ToList());

        public Task AddAsync(Recipe recipe, CancellationToken cancellationToken = default)
        {
            StoredRecipe = recipe;
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeRuleRepository(
        DietRule? rule = null,
        RuleVersion? version = null,
        RuleSetVersion? ruleSet = null) : IRuleCatalogRepository
    {
        public int SaveCount { get; private set; }

        public Task<IReadOnlyList<DietRule>> GetActiveRulesAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DietRule>>(rule is null ? [] : [rule]);

        public Task<IReadOnlyList<DietRule>> GetAllRulesAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DietRule>>(rule is null ? [] : [rule]);

        public Task<DietRule?> FindRuleAsync(
            Guid id,
            bool includeInactive,
            bool trackChanges = false,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(rule?.Id == id ? rule : null);

        public Task<RuleVersion?> FindVersionAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(version?.Id == id ? version : null);

        public Task<RuleSetVersion?> GetActiveRuleSetAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ruleSet);

        public Task<IReadOnlyList<RuleVersion>> FindVersionsAsync(
            IReadOnlyCollection<Guid> ids,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RuleVersion>>(
                version is not null && ids.Contains(version.Id) ? [version] : []);

        public Task AddRuleAsync(DietRule value, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task AddVersionAsync(RuleVersion value, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task AddRuleSetAsync(RuleSetVersion value, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
