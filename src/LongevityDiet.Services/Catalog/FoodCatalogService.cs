using LongevityDiet.Domain.Catalog.Entities;
using LongevityDiet.Repositories.Catalog.Queries;
using LongevityDiet.Repositories.Catalog.Repositories;

namespace LongevityDiet.Services.Catalog;

public sealed class FoodCatalogService(
    IFoodRepository foods,
    TimeProvider timeProvider)
{
    public async Task<CatalogServiceResult<PagedResult<FoodDto>>> QueryAsync(
        FoodQueryOptions options,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var queryResult = BuildQuery(options, includeInactive);
        if (!queryResult.Succeeded)
        {
            return CatalogServiceResult.Failure<PagedResult<FoodDto>>(
                queryResult.ErrorCode!,
                queryResult.ErrorMessage!);
        }

        var page = await foods.QueryAsync(queryResult.Value!, cancellationToken);
        var items = page.Items.Select(ToDto).ToList();

        return CatalogServiceResult.Success(
            PagedResultFactory.Create(items, options.Page, options.PageSize, page.TotalItems));
    }

    public async Task<CatalogServiceResult<FoodDto>> GetAsync(
        Guid id,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var food = await foods.FindByIdAsync(id, includeInactive, cancellationToken: cancellationToken);
        return food is null
            ? CatalogServiceResult.Failure<FoodDto>("food_not_found", "Food was not found.")
            : CatalogServiceResult.Success(ToDto(food));
    }

    public async Task<CatalogServiceResult<FoodDto>> CreateAsync(
        SaveFoodCommand command,
        CancellationToken cancellationToken = default)
    {
        var validation = Validate(command);
        if (validation is not null)
        {
            return CatalogServiceResult.Failure<FoodDto>(validation.Value.Code, validation.Value.Message);
        }

        var now = timeProvider.GetUtcNow();
        var food = new Food
        {
            Name = command.Name.Trim(),
            NormalizedName = NormalizeName(command.Name),
            Category = command.Category.Trim(),
            Tags = NormalizeOptional(command.Tags),
            Cuisine = NormalizeOptional(command.Cuisine),
            DefaultServingAmount = command.DefaultServingAmount,
            ServingUnit = command.ServingUnit.Trim(),
            Calories = command.Calories,
            ProteinG = command.ProteinG,
            CarbohydrateG = command.CarbohydrateG,
            FatG = command.FatG,
            SaturatedFatG = command.SaturatedFatG,
            SugarG = command.SugarG,
            FiberG = command.FiberG,
            IsPlantBased = command.IsPlantBased,
            IsLegume = command.IsLegume,
            IsWholeGrain = command.IsWholeGrain,
            IsFish = command.IsFish,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await foods.AddAsync(food, cancellationToken);
        await foods.SaveChangesAsync(cancellationToken);
        return CatalogServiceResult.Success(ToDto(food));
    }

    public async Task<CatalogServiceResult<FoodDto>> UpdateAsync(
        Guid id,
        SaveFoodCommand command,
        CancellationToken cancellationToken = default)
    {
        var validation = Validate(command);
        if (validation is not null)
        {
            return CatalogServiceResult.Failure<FoodDto>(validation.Value.Code, validation.Value.Message);
        }

        var food = await foods.FindByIdAsync(
            id,
            includeInactive: true,
            trackChanges: true,
            cancellationToken: cancellationToken);

        if (food is null)
        {
            return CatalogServiceResult.Failure<FoodDto>("food_not_found", "Food was not found.");
        }

        food.Name = command.Name.Trim();
        food.NormalizedName = NormalizeName(command.Name);
        food.Category = command.Category.Trim();
        food.Tags = NormalizeOptional(command.Tags);
        food.Cuisine = NormalizeOptional(command.Cuisine);
        food.DefaultServingAmount = command.DefaultServingAmount;
        food.ServingUnit = command.ServingUnit.Trim();
        food.Calories = command.Calories;
        food.ProteinG = command.ProteinG;
        food.CarbohydrateG = command.CarbohydrateG;
        food.FatG = command.FatG;
        food.SaturatedFatG = command.SaturatedFatG;
        food.SugarG = command.SugarG;
        food.FiberG = command.FiberG;
        food.IsPlantBased = command.IsPlantBased;
        food.IsLegume = command.IsLegume;
        food.IsWholeGrain = command.IsWholeGrain;
        food.IsFish = command.IsFish;
        food.UpdatedAt = timeProvider.GetUtcNow();

        await foods.SaveChangesAsync(cancellationToken);
        return CatalogServiceResult.Success(ToDto(food));
    }

    public async Task<CatalogServiceResult<FoodDto>> DeactivateAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var food = await foods.FindByIdAsync(
            id,
            includeInactive: true,
            trackChanges: true,
            cancellationToken: cancellationToken);

        if (food is null)
        {
            return CatalogServiceResult.Failure<FoodDto>("food_not_found", "Food was not found.");
        }

        food.IsActive = false;
        food.UpdatedAt = timeProvider.GetUtcNow();
        await foods.SaveChangesAsync(cancellationToken);
        return CatalogServiceResult.Success(ToDto(food));
    }

    private static CatalogServiceResult<FoodQuery> BuildQuery(
        FoodQueryOptions options,
        bool includeInactive)
    {
        if (options.Page < 1)
        {
            return CatalogServiceResult.Failure<FoodQuery>("invalid_page", "Page must be at least 1.");
        }

        if (options.PageSize is < 1 or > 100)
        {
            return CatalogServiceResult.Failure<FoodQuery>(
                "invalid_page_size",
                "Page size must be between 1 and 100.");
        }

        if (!TryParseSortField(options.SortBy, out var sortField))
        {
            return CatalogServiceResult.Failure<FoodQuery>(
                "invalid_sort_field",
                "Supported food sort fields are name, category, calories and createdAt.");
        }

        if (!TryParseDirection(options.SortDirection, out var direction))
        {
            return CatalogServiceResult.Failure<FoodQuery>(
                "invalid_sort_direction",
                "Sort direction must be asc or desc.");
        }

        return CatalogServiceResult.Success(new FoodQuery(
            options.Page,
            options.PageSize,
            NormalizeOptional(options.Search),
            NormalizeOptional(options.Category),
            options.IsPlantBased,
            options.IsLegume,
            options.IsWholeGrain,
            options.IsFish,
            !includeInactive,
            sortField,
            direction));
    }

    private static (string Code, string Message)? Validate(SaveFoodCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return ("invalid_name", "Food name is required.");
        }

        if (string.IsNullOrWhiteSpace(command.Category))
        {
            return ("invalid_category", "Food category is required.");
        }

        if (string.IsNullOrWhiteSpace(command.ServingUnit) || command.DefaultServingAmount <= 0)
        {
            return ("invalid_serving", "A positive serving amount and serving unit are required.");
        }

        var nutrition = new[]
        {
            command.Calories,
            command.ProteinG,
            command.CarbohydrateG,
            command.FatG,
            command.SaturatedFatG ?? 0,
            command.SugarG ?? 0,
            command.FiberG ?? 0,
        };

        return nutrition.Any(value => value < 0)
            ? ("invalid_nutrition", "Nutrition values cannot be negative.")
            : null;
    }

    private static bool TryParseSortField(string value, out FoodSortField sortField)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "name":
                sortField = FoodSortField.Name;
                return true;
            case "category":
                sortField = FoodSortField.Category;
                return true;
            case "calories":
                sortField = FoodSortField.Calories;
                return true;
            case "createdat":
                sortField = FoodSortField.CreatedAt;
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

    private static string NormalizeName(string name) => name.Trim().ToUpperInvariant();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static FoodDto ToDto(Food food) =>
        new(
            food.Id,
            food.Name,
            food.Category,
            food.Tags,
            food.Cuisine,
            food.DefaultServingAmount,
            food.ServingUnit,
            food.Calories,
            food.ProteinG,
            food.CarbohydrateG,
            food.FatG,
            food.SaturatedFatG,
            food.SugarG,
            food.FiberG,
            food.IsPlantBased,
            food.IsLegume,
            food.IsWholeGrain,
            food.IsFish,
            food.IsActive,
            food.CreatedAt,
            food.UpdatedAt);
}
