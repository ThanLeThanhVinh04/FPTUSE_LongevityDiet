namespace LongevityDiet.Domain.Entities;

public sealed class MealPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string RuleSetVersion { get; set; } = string.Empty;
    public int RevisionNumber { get; set; } = 1;
    public Guid? PreviousPlanId { get; set; }
    public MealPlanStatus Status { get; set; } = MealPlanStatus.Active;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int MealsPerDay { get; set; } = 3;
    public string TimeZone { get; set; } = "Asia/Ho_Chi_Minh";
    public string DietaryPattern { get; set; } = "PlantForward";
    public string AppliedAllergiesCsv { get; set; } = string.Empty;
    public string AppliedExclusionsCsv { get; set; } = string.Empty;
    public string ReasonCodesCsv { get; set; } = string.Empty;
    public string PlanExplanation { get; set; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<MealPlanDay> Days { get; set; } = new List<MealPlanDay>();
}

public sealed class MealPlanDay
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MealPlanId { get; set; }
    public int DayNumber { get; set; }
    public DateOnly PlanDate { get; set; }
    public int TargetCaloriesKcal { get; set; }
    public string DailyFocusCode { get; set; } = string.Empty;
    public string DailyFocusTitle { get; set; } = string.Empty;

    public MealPlan MealPlan { get; set; } = null!;
    public ICollection<PlannedMeal> Meals { get; set; } = new List<PlannedMeal>();
}

public sealed class PlannedMeal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MealPlanDayId { get; set; }
    public Guid MealPlanId { get; set; }
    public Guid UserId { get; set; }
    public int SlotOrder { get; set; }
    public MealSlotType SlotType { get; set; } = MealSlotType.Breakfast;
    public TimeOnly PlannedTime { get; set; }
    public string CandidateCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Cuisine { get; set; } = string.Empty;
    public decimal ServingSize { get; set; } = 1m;
    public string ServingUnit { get; set; } = "khẩu phần";
    public int CaloriesKcal { get; set; }
    public decimal ProteinGrams { get; set; }
    public decimal CarbsGrams { get; set; }
    public decimal FatGrams { get; set; }
    public decimal FiberGrams { get; set; }
    public bool IsPlantBased { get; set; }
    public bool HasLegumes { get; set; }
    public bool HasWholeGrains { get; set; }
    public bool ContainsFish { get; set; }
    public string AllergensCsv { get; set; } = string.Empty;
    public string IngredientsCsv { get; set; } = string.Empty;
    public decimal DeterministicScore { get; set; }
    public string ReasonCodesCsv { get; set; } = string.Empty;
    public string ExplanationSummary { get; set; } = string.Empty;
    public int RegenerationCount { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public MealPlanDay MealPlanDay { get; set; } = null!;
}

public enum MealPlanStatus
{
    Active = 1,
    Superseded = 2,
    Archived = 3
}

public enum MealSlotType
{
    Breakfast = 1,
    Lunch = 2,
    Dinner = 3,
    Snack = 4
}

