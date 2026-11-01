using System.ComponentModel.DataAnnotations;

namespace LongevityDiet.API.Contracts.Tracking;

public sealed record MealLogItemRequest(
    [MaxLength(80)] string? FoodCode,
    [Required, MinLength(1), MaxLength(200)] string FoodName,
    [Range(typeof(decimal), "0.1", "5000")] decimal ServingAmount,
    [Required, MaxLength(64)] string ServingUnit,
    [Range(0, 5000)] int CaloriesKcal,
    [Range(typeof(decimal), "0", "500")] decimal ProteinGrams,
    [Range(typeof(decimal), "0", "500")] decimal CarbsGrams,
    [Range(typeof(decimal), "0", "500")] decimal FatGrams,
    [Range(typeof(decimal), "0", "200")] decimal FiberGrams,
    bool IsPlantBased = true);

public sealed record CreateMealLogRequest(
    [Required] DateTimeOffset EatenAt,
    [Required, MaxLength(24)] string MealType,
    [Required, MinLength(2), MaxLength(200)] string Title,
    Guid? PlannedMealId,
    [MaxLength(1000)] string? Notes,
    [Required, MinLength(1)] IReadOnlyList<MealLogItemRequest> Items);

public sealed record UpdateMealLogRequest(
    [Required] DateTimeOffset EatenAt,
    [Required, MaxLength(24)] string MealType,
    [Required, MinLength(2), MaxLength(200)] string Title,
    Guid? PlannedMealId,
    [MaxLength(1000)] string? Notes,
    [Required, MinLength(1)] IReadOnlyList<MealLogItemRequest> Items);

public sealed record MealLogItemResponse(
    Guid Id,
    string? FoodCode,
    string FoodName,
    decimal ServingAmount,
    string ServingUnit,
    int CaloriesKcal,
    decimal ProteinGrams,
    decimal CarbsGrams,
    decimal FatGrams,
    decimal FiberGrams,
    bool IsPlantBased);

public sealed record MealLogResponse(
    Guid Id,
    Guid UserId,
    Guid? PlannedMealId,
    DateTimeOffset EatenAt,
    DateOnly LocalDate,
    TimeOnly LocalTime,
    string TimeZone,
    string MealType,
    string Title,
    string? Notes,
    int TotalCaloriesKcal,
    decimal TotalProteinGrams,
    decimal TotalCarbsGrams,
    decimal TotalFatGrams,
    decimal TotalFiberGrams,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<MealLogItemResponse> Items);

public sealed record EatingWindowSummaryResponse(
    Guid UserId,
    DateOnly LocalDate,
    string TimeZone,
    int LoggedMealCount,
    int CaloricMealCount,
    int TotalCaloriesKcal,
    DateTimeOffset? FirstMealAt,
    DateTimeOffset? LastMealAt,
    TimeOnly? FirstMealLocalTime,
    TimeOnly? LastMealLocalTime,
    int? EatingWindowMinutes,
    int TargetWindowMinutes,
    bool IsWithinTargetWindow,
    TimeOnly? ConfiguredSleepTime,
    int? MinutesBeforeBedtime,
    bool IsLateEatingFlagged,
    string StatusCode,
    string SummaryMessage,
    DateTimeOffset CalculatedAt);

