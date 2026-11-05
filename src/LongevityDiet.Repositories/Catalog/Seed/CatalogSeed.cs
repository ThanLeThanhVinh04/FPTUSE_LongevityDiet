using LongevityDiet.Domain.Catalog;
using LongevityDiet.Domain.Catalog.Entities;
using Microsoft.EntityFrameworkCore;

namespace LongevityDiet.Repositories.Catalog.Seed;

public static class CatalogSeed
{
    private static readonly DateTimeOffset SeedTimestamp =
        new(2026, 11, 2, 0, 0, 0, TimeSpan.Zero);

    private static readonly Guid LentilsId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid BrownRiceId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid SalmonId = Guid.Parse("10000000-0000-0000-0000-000000000003");
    private static readonly Guid BroccoliId = Guid.Parse("10000000-0000-0000-0000-000000000004");

    private static readonly Guid SoyAllergenId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid FishAllergenId = Guid.Parse("20000000-0000-0000-0000-000000000002");

    private static readonly Guid LentilRiceBowlId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid SalmonRicePlateId = Guid.Parse("30000000-0000-0000-0000-000000000002");

    private static readonly Guid PlantForwardRuleId = Guid.Parse("40000000-0000-0000-0000-000000000001");
    private static readonly Guid SugarRuleId = Guid.Parse("40000000-0000-0000-0000-000000000002");
    private static readonly Guid PlantForwardVersionId = Guid.Parse("50000000-0000-0000-0000-000000000001");
    private static readonly Guid SugarVersionId = Guid.Parse("50000000-0000-0000-0000-000000000002");
    private static readonly Guid InitialRuleSetId = Guid.Parse("60000000-0000-0000-0000-000000000001");

    public static async Task SeedAsync(
        LongevityDietDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        await SeedFoodsAndAllergensAsync(dbContext, cancellationToken);
        await SeedRecipesAsync(dbContext, cancellationToken);
        await SeedRulesAsync(dbContext, cancellationToken);
    }

    private static async Task SeedFoodsAndAllergensAsync(
        LongevityDietDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var existingFoodIds = await dbContext.Foods
            .Where(x => new[] { LentilsId, BrownRiceId, SalmonId, BroccoliId }.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var foods = CreateFoods()
            .Where(x => !existingFoodIds.Contains(x.Id))
            .ToList();

        if (foods.Count > 0)
        {
            await dbContext.Foods.AddRangeAsync(foods, cancellationToken);
        }

        var existingAllergenIds = await dbContext.Allergens
            .Where(x => new[] { SoyAllergenId, FishAllergenId }.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var allergens = CreateAllergens()
            .Where(x => !existingAllergenIds.Contains(x.Id))
            .ToList();

        if (allergens.Count > 0)
        {
            await dbContext.Allergens.AddRangeAsync(allergens, cancellationToken);
        }

        if (foods.Count > 0 || allergens.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task SeedRecipesAsync(
        LongevityDietDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Recipes.AnyAsync(x => x.Id == LentilRiceBowlId, cancellationToken))
        {
            var recipe = new Recipe
            {
                Id = LentilRiceBowlId,
                Name = "Lentil Brown Rice Bowl",
                NormalizedName = "LENTIL BROWN RICE BOWL",
                Description = "Team-authored bowl with lentils, brown rice and broccoli.",
                Category = "Main",
                Cuisine = "International",
                Tags = "plant-forward,legume,whole-grain",
                ServingCount = 2,
                PrepMinutes = 25,
                CaloriesPerServing = 430m,
                ProteinGPerServing = 18m,
                CarbohydrateGPerServing = 72m,
                FatGPerServing = 8m,
                SaturatedFatGPerServing = 1.2m,
                SugarGPerServing = 5m,
                FiberGPerServing = 12m,
                IsPlantForward = true,
                HasLegume = true,
                HasWholeGrain = true,
                HasFish = false,
                IsActive = true,
                CreatedAt = SeedTimestamp,
                UpdatedAt = SeedTimestamp,
                Ingredients =
                [
                    new RecipeIngredient
                    {
                        RecipeId = LentilRiceBowlId,
                        FoodId = LentilsId,
                        Amount = 160m,
                        Unit = "g",
                    },
                    new RecipeIngredient
                    {
                        RecipeId = LentilRiceBowlId,
                        FoodId = BrownRiceId,
                        Amount = 140m,
                        Unit = "g",
                    },
                    new RecipeIngredient
                    {
                        RecipeId = LentilRiceBowlId,
                        FoodId = BroccoliId,
                        Amount = 120m,
                        Unit = "g",
                    },
                ],
            };

            await dbContext.Recipes.AddAsync(recipe, cancellationToken);
        }

        if (!await dbContext.Recipes.AnyAsync(x => x.Id == SalmonRicePlateId, cancellationToken))
        {
            var recipe = new Recipe
            {
                Id = SalmonRicePlateId,
                Name = "Salmon Brown Rice Plate",
                NormalizedName = "SALMON BROWN RICE PLATE",
                Description = "Team-authored plate with salmon, brown rice and broccoli.",
                Category = "Main",
                Cuisine = "International",
                Tags = "fish,whole-grain",
                ServingCount = 2,
                PrepMinutes = 30,
                CaloriesPerServing = 510m,
                ProteinGPerServing = 31m,
                CarbohydrateGPerServing = 48m,
                FatGPerServing = 20m,
                SaturatedFatGPerServing = 4m,
                SugarGPerServing = 4m,
                FiberGPerServing = 7m,
                IsPlantForward = false,
                HasLegume = false,
                HasWholeGrain = true,
                HasFish = true,
                IsActive = true,
                CreatedAt = SeedTimestamp,
                UpdatedAt = SeedTimestamp,
                Ingredients =
                [
                    new RecipeIngredient
                    {
                        RecipeId = SalmonRicePlateId,
                        FoodId = SalmonId,
                        Amount = 140m,
                        Unit = "g",
                    },
                    new RecipeIngredient
                    {
                        RecipeId = SalmonRicePlateId,
                        FoodId = BrownRiceId,
                        Amount = 140m,
                        Unit = "g",
                    },
                    new RecipeIngredient
                    {
                        RecipeId = SalmonRicePlateId,
                        FoodId = BroccoliId,
                        Amount = 120m,
                        Unit = "g",
                    },
                ],
                Allergens =
                [
                    new RecipeAllergen
                    {
                        RecipeId = SalmonRicePlateId,
                        AllergenId = FishAllergenId,
                    },
                ],
            };

            await dbContext.Recipes.AddAsync(recipe, cancellationToken);
        }

        if (dbContext.ChangeTracker.HasChanges())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task SeedRulesAsync(
        LongevityDietDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var plantRule = await dbContext.DietRules
            .Include(x => x.Versions)
            .SingleOrDefaultAsync(x => x.Id == PlantForwardRuleId, cancellationToken);

        if (plantRule is null)
        {
            plantRule = new DietRule
            {
                Id = PlantForwardRuleId,
                RuleKey = "PLANT_FORWARD_PATTERN",
                Title = "Prefer a plant-forward pattern",
                Description = "Favor vegetables, legumes and whole grains while keeping the rule educational and configurable.",
                Category = "FoodPattern",
                IsMedicalSensitive = false,
                IsActive = true,
                CreatedAt = SeedTimestamp,
                UpdatedAt = SeedTimestamp,
            };

            await dbContext.DietRules.AddAsync(plantRule, cancellationToken);
        }

        var plantVersion = await dbContext.RuleVersions
            .SingleOrDefaultAsync(x => x.Id == PlantForwardVersionId, cancellationToken);

        if (plantVersion is null)
        {
            plantVersion = new RuleVersion
            {
                Id = PlantForwardVersionId,
                DietRuleId = PlantForwardRuleId,
                VersionNumber = 1,
                ParametersJson = "{\"plantForwardBoost\":1}",
                SourceUrl = "https://www.fondazionevalterlongo.org/en/longevity-diet-for-adults/",
                SourceType = SourceType.OfficialGuidance,
                Citation = "Fondazione Valter Longo, Longevity Diet for Adults",
                EvidenceLevel = EvidenceLevel.Moderate,
                State = RuleVersionState.Published,
                EffectiveAt = SeedTimestamp,
                PublishedAt = SeedTimestamp,
                CreatedAt = SeedTimestamp,
            };

            await dbContext.RuleVersions.AddAsync(plantVersion, cancellationToken);
        }

        var sugarRule = await dbContext.DietRules
            .Include(x => x.Versions)
            .SingleOrDefaultAsync(x => x.Id == SugarRuleId, cancellationToken);

        if (sugarRule is null)
        {
            sugarRule = new DietRule
            {
                Id = SugarRuleId,
                RuleKey = "LIMIT_REFINED_SUGAR",
                Title = "Limit refined sugar",
                Description = "Use sugar as a ranking signal for everyday meal choices, not as medical advice.",
                Category = "Nutrition",
                IsMedicalSensitive = false,
                IsActive = true,
                CreatedAt = SeedTimestamp,
                UpdatedAt = SeedTimestamp,
            };

            await dbContext.DietRules.AddAsync(sugarRule, cancellationToken);
        }

        var sugarVersion = await dbContext.RuleVersions
            .SingleOrDefaultAsync(x => x.Id == SugarVersionId, cancellationToken);

        if (sugarVersion is null)
        {
            sugarVersion = new RuleVersion
            {
                Id = SugarVersionId,
                DietRuleId = SugarRuleId,
                VersionNumber = 1,
                ParametersJson = "{\"rankingPenalty\":1}",
                SourceUrl = "https://www.fondazionevalterlongo.org/en/longevity-diet-for-adults/",
                SourceType = SourceType.OfficialGuidance,
                Citation = "Fondazione Valter Longo, Longevity Diet for Adults",
                EvidenceLevel = EvidenceLevel.Moderate,
                State = RuleVersionState.Published,
                EffectiveAt = SeedTimestamp,
                PublishedAt = SeedTimestamp,
                CreatedAt = SeedTimestamp,
            };

            await dbContext.RuleVersions.AddAsync(sugarVersion, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var ruleSet = await dbContext.RuleSetVersions
            .Include(x => x.RuleVersions)
            .SingleOrDefaultAsync(x => x.Id == InitialRuleSetId, cancellationToken);

        if (ruleSet is null)
        {
            ruleSet = new RuleSetVersion
            {
                Id = InitialRuleSetId,
                VersionLabel = "2026.11-initial",
                IsActive = true,
                EffectiveAt = SeedTimestamp,
                PublishedAt = SeedTimestamp,
                CreatedAt = SeedTimestamp,
            };

            ruleSet.RuleVersions.Add(plantVersion);
            ruleSet.RuleVersions.Add(sugarVersion);
            await dbContext.RuleSetVersions.AddAsync(ruleSet, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static IReadOnlyList<Food> CreateFoods() =>
    [
        new()
        {
            Id = LentilsId,
            Name = "Cooked Lentils",
            NormalizedName = "COOKED LENTILS",
            Category = "Legume",
            Tags = "plant-forward,protein,fiber",
            Cuisine = "International",
            DefaultServingAmount = 100m,
            ServingUnit = "g",
            Calories = 116m,
            ProteinG = 9m,
            CarbohydrateG = 20m,
            FatG = 0.4m,
            SaturatedFatG = 0.1m,
            SugarG = 1.8m,
            FiberG = 7.9m,
            IsPlantBased = true,
            IsLegume = true,
            IsWholeGrain = false,
            IsFish = false,
            IsActive = true,
            CreatedAt = SeedTimestamp,
            UpdatedAt = SeedTimestamp,
        },
        new()
        {
            Id = BrownRiceId,
            Name = "Cooked Brown Rice",
            NormalizedName = "COOKED BROWN RICE",
            Category = "Whole Grain",
            Tags = "whole-grain",
            Cuisine = "International",
            DefaultServingAmount = 100m,
            ServingUnit = "g",
            Calories = 123m,
            ProteinG = 2.7m,
            CarbohydrateG = 25.6m,
            FatG = 1m,
            SaturatedFatG = 0.3m,
            SugarG = 0.2m,
            FiberG = 1.6m,
            IsPlantBased = true,
            IsLegume = false,
            IsWholeGrain = true,
            IsFish = false,
            IsActive = true,
            CreatedAt = SeedTimestamp,
            UpdatedAt = SeedTimestamp,
        },
        new()
        {
            Id = SalmonId,
            Name = "Cooked Salmon",
            NormalizedName = "COOKED SALMON",
            Category = "Fish",
            Tags = "fish,protein",
            Cuisine = "International",
            DefaultServingAmount = 100m,
            ServingUnit = "g",
            Calories = 206m,
            ProteinG = 22m,
            CarbohydrateG = 0m,
            FatG = 12m,
            SaturatedFatG = 3m,
            SugarG = 0m,
            FiberG = 0m,
            IsPlantBased = false,
            IsLegume = false,
            IsWholeGrain = false,
            IsFish = true,
            IsActive = true,
            CreatedAt = SeedTimestamp,
            UpdatedAt = SeedTimestamp,
        },
        new()
        {
            Id = BroccoliId,
            Name = "Steamed Broccoli",
            NormalizedName = "STEAMED BROCCOLI",
            Category = "Vegetable",
            Tags = "plant-forward,vegetable",
            Cuisine = "International",
            DefaultServingAmount = 100m,
            ServingUnit = "g",
            Calories = 35m,
            ProteinG = 2.4m,
            CarbohydrateG = 7.2m,
            FatG = 0.4m,
            SaturatedFatG = 0.1m,
            SugarG = 1.4m,
            FiberG = 3.3m,
            IsPlantBased = true,
            IsLegume = false,
            IsWholeGrain = false,
            IsFish = false,
            IsActive = true,
            CreatedAt = SeedTimestamp,
            UpdatedAt = SeedTimestamp,
        },
    ];

    private static IReadOnlyList<Allergen> CreateAllergens() =>
    [
        new()
        {
            Id = SoyAllergenId,
            Code = "SOY",
            Name = "Soy",
            IsActive = true,
        },
        new()
        {
            Id = FishAllergenId,
            Code = "FISH",
            Name = "Fish",
            IsActive = true,
        },
    ];
}
