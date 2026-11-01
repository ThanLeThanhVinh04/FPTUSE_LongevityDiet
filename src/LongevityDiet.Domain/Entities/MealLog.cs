namespace LongevityDiet.Domain.Entities;

public sealed class MealLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid? PlannedMealId { get; set; }
    public DateTimeOffset EatenAt { get; set; }
    public DateOnly LocalDate { get; set; }
    public string TimeZone { get; set; } = "Asia/Ho_Chi_Minh";
    public MealSlotType MealType { get; set; } = MealSlotType.Breakfast;
    public string Title { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public int TotalCaloriesKcal { get; set; }
    public decimal TotalProteinGrams { get; set; }
    public decimal TotalCarbsGrams { get; set; }
    public decimal TotalFatGrams { get; set; }
    public decimal TotalFiberGrams { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<MealLogItem> Items { get; set; } = new List<MealLogItem>();
}

public sealed class MealLogItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MealLogId { get; set; }
    public string? FoodCode { get; set; }
    public string FoodName { get; set; } = string.Empty;
    public decimal ServingAmount { get; set; } = 1m;
    public string ServingUnit { get; set; } = "khẩu phần";
    public int CaloriesKcal { get; set; }
    public decimal ProteinGrams { get; set; }
    public decimal CarbsGrams { get; set; }
    public decimal FatGrams { get; set; }
    public decimal FiberGrams { get; set; }
    public bool IsPlantBased { get; set; } = true;

    public MealLog MealLog { get; set; } = null!;
}

