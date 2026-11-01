namespace LongevityDiet.Domain.Entities;

public sealed class EatingWindowSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public DateOnly LocalDate { get; set; }
    public string TimeZone { get; set; } = "Asia/Ho_Chi_Minh";
    public int LoggedMealCount { get; set; }
    public int CaloricMealCount { get; set; }
    public int TotalCaloriesKcal { get; set; }
    public DateTimeOffset? FirstMealAt { get; set; }
    public DateTimeOffset? LastMealAt { get; set; }
    public TimeOnly? FirstMealLocalTime { get; set; }
    public TimeOnly? LastMealLocalTime { get; set; }
    public int? EatingWindowMinutes { get; set; }
    public int TargetWindowMinutes { get; set; } = 720;
    public bool IsWithinTargetWindow { get; set; } = true;
    public TimeOnly? ConfiguredSleepTime { get; set; }
    public int? MinutesBeforeBedtime { get; set; }
    public bool IsLateEatingFlagged { get; set; }
    public string StatusCode { get; set; } = "NoMeals";
    public string SummaryMessage { get; set; } = string.Empty;
    public DateTimeOffset CalculatedAt { get; set; } = DateTimeOffset.UtcNow;
}

