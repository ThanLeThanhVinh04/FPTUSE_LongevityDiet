using LongevityDiet.Domain.Entities;

namespace LongevityDiet.Services.Tracking;

public sealed record UserScheduleSnapshot(
    string ContractVersion,
    Guid UserId,
    string TimeZone,
    TimeOnly? WakeTime,
    TimeOnly? SleepTime,
    int TargetWindowMinutes);

public sealed record MealLogItemInput(
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

public sealed record CreateMealLogCommand(
    DateTimeOffset EatenAt,
    string MealType,
    string Title,
    Guid? PlannedMealId,
    string? Notes,
    IReadOnlyList<MealLogItemInput> Items);

public sealed record UpdateMealLogCommand(
    DateTimeOffset EatenAt,
    string MealType,
    string Title,
    Guid? PlannedMealId,
    string? Notes,
    IReadOnlyList<MealLogItemInput> Items);

public sealed record MealLogItemSummary(
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

public sealed record MealLogSummary(
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
    IReadOnlyList<MealLogItemSummary> Items);

public sealed record EatingWindowSummary(
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

