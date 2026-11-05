using LongevityDiet.Domain.Catalog.Entities;
using LongevityDiet.Repositories.Catalog.Queries;
using LongevityDiet.Repositories.Catalog.Repositories;

namespace LongevityDiet.Services.Catalog;

public sealed class RecipeCatalogService(
    IRecipeRepository recipes,
    TimeProvider timeProvider)
{
    public async Task<CatalogServiceResult<IReadOnlyList<AllergenDto>>> GetActiveAllergensAsync(
        CancellationToken cancellationToken = default)
    {
        var allergens = await recipes.GetActiveAllergensAsync(cancellationToken);
        return CatalogServiceResult.Success<IReadOnlyList<AllergenDto>>(
            allergens.Select(x => new AllergenDto(x.Id, x.Code, x.Name)).ToList());
    }

    public async Task<CatalogServiceResult<PagedResult<RecipeSummaryDto>>> QueryAsync(
        RecipeQueryOptions options,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(options, includeInactive);
        if (!query.Succeeded)
        {
            return CatalogServiceResult.Failure<PagedResult<RecipeSummaryDto>>(
                query.ErrorCode!,
                query.ErrorMessage!);
        }

        var page = await recipes.QueryAsync(query.Value!, cancellationToken);
        return CatalogServiceResult.Success(
            PagedResultFactory.Create(
                page.Items.Select(ToSummary).ToList(),
                options.Page,
                options.PageSize,
                page.TotalItems));
    }

    public async Task<CatalogServiceResult<RecipeDetailDto>> GetAsync(
        Guid id,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var recipe = await recipes.FindByIdAsync(
            id,
            includeInactive,
            cancellationToken: cancellationToken);

        return recipe is null
            ? CatalogServiceResult.Failure<RecipeDetailDto>("recipe_not_found", "Recipe was not found.")
            : CatalogServiceResult.Success(ToDetail(recipe));
    }

    public async Task<CatalogServiceResult<RecipeDetailDto>> CreateAsync(
        SaveRecipeCommand command,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateAsync(command, cancellationToken);
        if (!validation.Succeeded)
        {
            return CatalogServiceResult.Failure<RecipeDetailDto>(
                validation.ErrorCode!,
                validation.ErrorMessage!);
        }

        var now = timeProvider.GetUtcNow();
        var recipe = new Recipe
        {
            Name = command.Name.Trim(),
            NormalizedName = command.Name.Trim().ToUpperInvariant(),
            Description = command.Description.Trim(),
            Category = command.Category.Trim(),
            Cuisine = NormalizeOptional(command.Cuisine),
            Tags = NormalizeOptional(command.Tags),
            ServingCount = command.ServingCount,
            PrepMinutes = command.PrepMinutes,
            CaloriesPerServing = command.CaloriesPerServing,
            ProteinGPerServing = command.ProteinGPerServing,
            CarbohydrateGPerServing = command.CarbohydrateGPerServing,
            FatGPerServing = command.FatGPerServing,
            SaturatedFatGPerServing = command.SaturatedFatGPerServing,
            SugarGPerServing = command.SugarGPerServing,
            FiberGPerServing = command.FiberGPerServing,
            IsPlantForward = command.IsPlantForward,
            HasLegume = command.HasLegume,
            HasWholeGrain = command.HasWholeGrain,
            HasFish = command.HasFish,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };

        ApplyIngredientsAndAllergens(recipe, command);
        await recipes.AddAsync(recipe, cancellationToken);
        await recipes.SaveChangesAsync(cancellationToken);

        var created = await recipes.FindByIdAsync(
            recipe.Id,
            includeInactive: true,
            cancellationToken: cancellationToken);

        return CatalogServiceResult.Success(ToDetail(created ?? recipe));
    }

    public async Task<CatalogServiceResult<RecipeDetailDto>> UpdateAsync(
        Guid id,
        SaveRecipeCommand command,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateAsync(command, cancellationToken);
        if (!validation.Succeeded)
        {
            return CatalogServiceResult.Failure<RecipeDetailDto>(
                validation.ErrorCode!,
                validation.ErrorMessage!);
        }

        var recipe = await recipes.FindByIdAsync(
            id,
            includeInactive: true,
            trackChanges: true,
            cancellationToken: cancellationToken);

        if (recipe is null)
        {
            return CatalogServiceResult.Failure<RecipeDetailDto>(
                "recipe_not_found",
                "Recipe was not found.");
        }

        recipe.Name = command.Name.Trim();
        recipe.NormalizedName = command.Name.Trim().ToUpperInvariant();
        recipe.Description = command.Description.Trim();
        recipe.Category = command.Category.Trim();
        recipe.Cuisine = NormalizeOptional(command.Cuisine);
        recipe.Tags = NormalizeOptional(command.Tags);
        recipe.ServingCount = command.ServingCount;
        recipe.PrepMinutes = command.PrepMinutes;
        recipe.CaloriesPerServing = command.CaloriesPerServing;
        recipe.ProteinGPerServing = command.ProteinGPerServing;
        recipe.CarbohydrateGPerServing = command.CarbohydrateGPerServing;
        recipe.FatGPerServing = command.FatGPerServing;
        recipe.SaturatedFatGPerServing = command.SaturatedFatGPerServing;
        recipe.SugarGPerServing = command.SugarGPerServing;
        recipe.FiberGPerServing = command.FiberGPerServing;
        recipe.IsPlantForward = command.IsPlantForward;
        recipe.HasLegume = command.HasLegume;
        recipe.HasWholeGrain = command.HasWholeGrain;
        recipe.HasFish = command.HasFish;
        recipe.UpdatedAt = timeProvider.GetUtcNow();

        SyncIngredientsAndAllergens(recipe, command);

        await recipes.SaveChangesAsync(cancellationToken);

        var updated = await recipes.FindByIdAsync(
            recipe.Id,
            includeInactive: true,
            cancellationToken: cancellationToken);

        return CatalogServiceResult.Success(ToDetail(updated ?? recipe));
    }

    public async Task<CatalogServiceResult<RecipeDetailDto>> DeactivateAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var recipe = await recipes.FindByIdAsync(
            id,
            includeInactive: true,
            trackChanges: true,
            cancellationToken: cancellationToken);

        if (recipe is null)
        {
            return CatalogServiceResult.Failure<RecipeDetailDto>(
                "recipe_not_found",
                "Recipe was not found.");
        }

        recipe.IsActive = false;
        recipe.UpdatedAt = timeProvider.GetUtcNow();
        await recipes.SaveChangesAsync(cancellationToken);
        return CatalogServiceResult.Success(ToDetail(recipe));
    }

    private async Task<CatalogServiceResult<bool>> ValidateAsync(
        SaveRecipeCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Name) ||
            string.IsNullOrWhiteSpace(command.Description) ||
            string.IsNullOrWhiteSpace(command.Category))
        {
            return CatalogServiceResult.Failure<bool>(
                "invalid_recipe",
                "Recipe name, description and category are required.");
        }

        if (command.ServingCount < 1 || command.PrepMinutes < 0)
        {
            return CatalogServiceResult.Failure<bool>(
                "invalid_recipe_measurement",
                "Serving count must be positive and prep minutes cannot be negative.");
        }

        var nutrition = new[]
        {
            command.CaloriesPerServing,
            command.ProteinGPerServing,
            command.CarbohydrateGPerServing,
            command.FatGPerServing,
            command.SaturatedFatGPerServing ?? 0,
            command.SugarGPerServing ?? 0,
            command.FiberGPerServing ?? 0,
        };

        if (nutrition.Any(value => value < 0))
        {
            return CatalogServiceResult.Failure<bool>(
                "invalid_nutrition",
                "Nutrition values cannot be negative.");
        }

        if (command.Ingredients.Count == 0 ||
            command.Ingredients.Any(x => x.FoodId == Guid.Empty || x.Amount <= 0 || string.IsNullOrWhiteSpace(x.Unit)))
        {
            return CatalogServiceResult.Failure<bool>(
                "invalid_ingredients",
                "Recipe must contain valid ingredients with positive amounts.");
        }

        var foodIds = command.Ingredients.Select(x => x.FoodId).Distinct().ToArray();
        if (foodIds.Length != command.Ingredients.Count)
        {
            return CatalogServiceResult.Failure<bool>(
                "duplicate_ingredient",
                "Each food can appear only once in a recipe.");
        }

        var foods = await recipes.FindFoodsAsync(foodIds, cancellationToken);
        if (foods.Count != foodIds.Length)
        {
            return CatalogServiceResult.Failure<bool>(
                "invalid_food_reference",
                "One or more ingredient foods do not exist or are inactive.");
        }

        var allergenIds = command.AllergenIds.Distinct().ToArray();
        if (allergenIds.Length != command.AllergenIds.Count)
        {
            return CatalogServiceResult.Failure<bool>(
                "duplicate_allergen",
                "Each allergen can appear only once in a recipe.");
        }

        if (allergenIds.Length > 0)
        {
            var allergens = await recipes.FindAllergensAsync(allergenIds, cancellationToken);
            if (allergens.Count != allergenIds.Length)
            {
                return CatalogServiceResult.Failure<bool>(
                    "invalid_allergen_reference",
                    "One or more allergens do not exist or are inactive.");
            }
        }

        return CatalogServiceResult.Success(true);
    }

    private static CatalogServiceResult<RecipeQuery> BuildQuery(
        RecipeQueryOptions options,
        bool includeInactive)
    {
        if (options.Page < 1)
        {
            return CatalogServiceResult.Failure<RecipeQuery>("invalid_page", "Page must be at least 1.");
        }

        if (options.PageSize is < 1 or > 100)
        {
            return CatalogServiceResult.Failure<RecipeQuery>(
                "invalid_page_size",
                "Page size must be between 1 and 100.");
        }

        if (!TryParseSortField(options.SortBy, out var sortField))
        {
            return CatalogServiceResult.Failure<RecipeQuery>(
                "invalid_sort_field",
                "Supported recipe sort fields are name, category, cuisine, prepMinutes and createdAt.");
        }

        if (!TryParseDirection(options.SortDirection, out var direction))
        {
            return CatalogServiceResult.Failure<RecipeQuery>(
                "invalid_sort_direction",
                "Sort direction must be asc or desc.");
        }

        return CatalogServiceResult.Success(new RecipeQuery(
            options.Page,
            options.PageSize,
            NormalizeOptional(options.Search),
            NormalizeOptional(options.Category),
            NormalizeOptional(options.Cuisine),
            options.IsPlantForward,
            options.HasLegume,
            options.HasWholeGrain,
            options.HasFish,
            !includeInactive,
            sortField,
            direction));
    }

    private static void ApplyIngredientsAndAllergens(Recipe recipe, SaveRecipeCommand command)
    {
        foreach (var ingredient in command.Ingredients)
        {
            recipe.Ingredients.Add(new RecipeIngredient
            {
                RecipeId = recipe.Id,
                FoodId = ingredient.FoodId,
                Amount = ingredient.Amount,
                Unit = ingredient.Unit.Trim(),
                IsOptional = ingredient.IsOptional,
            });
        }

        foreach (var allergenId in command.AllergenIds)
        {
            recipe.Allergens.Add(new RecipeAllergen
            {
                RecipeId = recipe.Id,
                AllergenId = allergenId,
            });
        }
    }

    private static void SyncIngredientsAndAllergens(Recipe recipe, SaveRecipeCommand command)
    {
        var requestedFoodIds = command.Ingredients.Select(x => x.FoodId).ToHashSet();
        foreach (var existing in recipe.Ingredients
                     .Where(x => !requestedFoodIds.Contains(x.FoodId))
                     .ToList())
        {
            recipe.Ingredients.Remove(existing);
        }

        foreach (var requested in command.Ingredients)
        {
            var existing = recipe.Ingredients.SingleOrDefault(x => x.FoodId == requested.FoodId);
            if (existing is null)
            {
                recipe.Ingredients.Add(new RecipeIngredient
                {
                    RecipeId = recipe.Id,
                    FoodId = requested.FoodId,
                    Amount = requested.Amount,
                    Unit = requested.Unit.Trim(),
                    IsOptional = requested.IsOptional,
                });
                continue;
            }

            existing.Amount = requested.Amount;
            existing.Unit = requested.Unit.Trim();
            existing.IsOptional = requested.IsOptional;
        }

        var requestedAllergenIds = command.AllergenIds.ToHashSet();
        foreach (var existing in recipe.Allergens
                     .Where(x => !requestedAllergenIds.Contains(x.AllergenId))
                     .ToList())
        {
            recipe.Allergens.Remove(existing);
        }

        foreach (var allergenId in requestedAllergenIds)
        {
            if (recipe.Allergens.All(x => x.AllergenId != allergenId))
            {
                recipe.Allergens.Add(new RecipeAllergen
                {
                    RecipeId = recipe.Id,
                    AllergenId = allergenId,
                });
            }
        }
    }

    private static bool TryParseSortField(string value, out RecipeSortField sortField)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "name":
                sortField = RecipeSortField.Name;
                return true;
            case "category":
                sortField = RecipeSortField.Category;
                return true;
            case "cuisine":
                sortField = RecipeSortField.Cuisine;
                return true;
            case "prepminutes":
                sortField = RecipeSortField.PrepMinutes;
                return true;
            case "createdat":
                sortField = RecipeSortField.CreatedAt;
                return true;
            default:
                sortField = default;
                return false;
        }
    }

    private static bool TryParseDirection(string value, out SortDirection direction)
    {
        if (string.Equals(value, "asc", StringComparison.OrdinalIgnoreCase))
        {
            direction = SortDirection.Asc;
            return true;
        }

        if (string.Equals(value, "desc", StringComparison.OrdinalIgnoreCase))
        {
            direction = SortDirection.Desc;
            return true;
        }

        direction = default;
        return false;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static RecipeSummaryDto ToSummary(Recipe recipe) =>
        new(
            recipe.Id,
            recipe.Name,
            recipe.Category,
            recipe.Cuisine,
            recipe.Tags,
            recipe.ServingCount,
            recipe.PrepMinutes,
            recipe.CaloriesPerServing,
            recipe.IsPlantForward,
            recipe.HasLegume,
            recipe.HasWholeGrain,
            recipe.HasFish,
            recipe.IsActive);

    private static RecipeDetailDto ToDetail(Recipe recipe) =>
        new(
            recipe.Id,
            recipe.Name,
            recipe.Description,
            recipe.Category,
            recipe.Cuisine,
            recipe.Tags,
            recipe.ServingCount,
            recipe.PrepMinutes,
            recipe.CaloriesPerServing,
            recipe.ProteinGPerServing,
            recipe.CarbohydrateGPerServing,
            recipe.FatGPerServing,
            recipe.SaturatedFatGPerServing,
            recipe.SugarGPerServing,
            recipe.FiberGPerServing,
            recipe.IsPlantForward,
            recipe.HasLegume,
            recipe.HasWholeGrain,
            recipe.HasFish,
            recipe.IsActive,
            recipe.Ingredients
                .OrderBy(x => x.Food.Name)
                .Select(x => new RecipeIngredientDto(
                    x.FoodId,
                    x.Food?.Name ?? string.Empty,
                    x.Amount,
                    x.Unit,
                    x.IsOptional))
                .ToList(),
            recipe.Allergens
                .OrderBy(x => x.Allergen.Code)
                .Select(x => new AllergenDto(
                    x.AllergenId,
                    x.Allergen?.Code ?? string.Empty,
                    x.Allergen?.Name ?? string.Empty))
                .ToList(),
            recipe.CreatedAt,
            recipe.UpdatedAt);
}
