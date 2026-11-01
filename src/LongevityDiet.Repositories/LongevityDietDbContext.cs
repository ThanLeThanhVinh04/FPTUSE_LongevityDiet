using LongevityDiet.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LongevityDiet.Repositories;

public sealed class LongevityDietDbContext(DbContextOptions<LongevityDietDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserDietaryPreference> UserDietaryPreferences => Set<UserDietaryPreference>();
    public DbSet<MealPlan> MealPlans => Set<MealPlan>();
    public DbSet<MealPlanDay> MealPlanDays => Set<MealPlanDay>();
    public DbSet<PlannedMeal> PlannedMeals => Set<PlannedMeal>();
    public DbSet<MealLog> MealLogs => Set<MealLog>();
    public DbSet<MealLogItem> MealLogItems => Set<MealLogItem>();
    public DbSet<EatingWindowSnapshot> EatingWindowSnapshots => Set<EatingWindowSnapshot>();
    public DbSet<RecommendationFeedback> RecommendationFeedbacks => Set<RecommendationFeedback>();
    public DbSet<MealReplacementHistory> MealReplacementHistories => Set<MealReplacementHistory>();
    public DbSet<UserFoodPreference> UserFoodPreferences => Set<UserFoodPreference>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var user = modelBuilder.Entity<User>();
        user.ToTable("Users");
        user.HasKey(x => x.Id);
        user.Property(x => x.Email).HasMaxLength(320).IsRequired();
        user.Property(x => x.NormalizedEmail).HasMaxLength(320).IsRequired();
        user.HasIndex(x => x.NormalizedEmail).IsUnique();
        user.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
        user.Property(x => x.DisplayName).HasMaxLength(120).IsRequired();
        user.Property(x => x.Role).HasConversion<string>().HasMaxLength(24);
        user.Property(x => x.Status).HasConversion<string>().HasMaxLength(24);
        user.HasOne(x => x.Profile)
            .WithOne(x => x.User)
            .HasForeignKey<UserProfile>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        var profile = modelBuilder.Entity<UserProfile>();
        profile.ToTable("UserProfiles");
        profile.HasKey(x => x.UserId);
        profile.Property(x => x.TimeZone).HasMaxLength(100).IsRequired();
        profile.Property(x => x.FoodPreference).HasMaxLength(500);

        var refreshToken = modelBuilder.Entity<RefreshToken>();
        refreshToken.ToTable("RefreshTokens");
        refreshToken.HasKey(x => x.Id);
        refreshToken.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        refreshToken.HasIndex(x => x.TokenHash).IsUnique();
        refreshToken.HasIndex(x => new { x.UserId, x.ExpiresAt });
        refreshToken.HasOne(x => x.User)
            .WithMany(x => x.RefreshTokens)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        ConfigurePlanningModel(modelBuilder);
        ConfigureTrackingModel(modelBuilder);
        ConfigureRecommendationModel(modelBuilder);
    }

    private static void ConfigurePlanningModel(ModelBuilder modelBuilder)
    {
        var dietaryPreference = modelBuilder.Entity<UserDietaryPreference>();
        dietaryPreference.ToTable("UserDietaryPreferences");
        dietaryPreference.HasKey(x => x.UserId);
        dietaryPreference.Property(x => x.DietaryPattern).HasMaxLength(64).IsRequired();
        dietaryPreference.Property(x => x.AllergiesCsv).HasMaxLength(1000).IsRequired();
        dietaryPreference.Property(x => x.ExcludedFoodsCsv).HasMaxLength(1000).IsRequired();
        dietaryPreference.Property(x => x.PreferredCuisine).HasMaxLength(120);

        var mealPlan = modelBuilder.Entity<MealPlan>();
        mealPlan.ToTable("MealPlans");
        mealPlan.HasKey(x => x.Id);
        mealPlan.Property(x => x.RuleSetVersion).HasMaxLength(64).IsRequired();
        mealPlan.Property(x => x.Status).HasConversion<string>().HasMaxLength(24);
        mealPlan.Property(x => x.TimeZone).HasMaxLength(100).IsRequired();
        mealPlan.Property(x => x.DietaryPattern).HasMaxLength(64).IsRequired();
        mealPlan.Property(x => x.AppliedAllergiesCsv).HasMaxLength(1000).IsRequired();
        mealPlan.Property(x => x.AppliedExclusionsCsv).HasMaxLength(1000).IsRequired();
        mealPlan.Property(x => x.ReasonCodesCsv).HasMaxLength(500).IsRequired();
        mealPlan.Property(x => x.PlanExplanation).HasMaxLength(1000).IsRequired();
        mealPlan.HasIndex(x => new { x.UserId, x.Status, x.GeneratedAt });
        mealPlan.HasMany(x => x.Days)
            .WithOne(x => x.MealPlan)
            .HasForeignKey(x => x.MealPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        var mealPlanDay = modelBuilder.Entity<MealPlanDay>();
        mealPlanDay.ToTable("MealPlanDays");
        mealPlanDay.HasKey(x => x.Id);
        mealPlanDay.Property(x => x.DailyFocusCode).HasMaxLength(64).IsRequired();
        mealPlanDay.Property(x => x.DailyFocusTitle).HasMaxLength(240).IsRequired();
        mealPlanDay.HasIndex(x => new { x.MealPlanId, x.DayNumber }).IsUnique();
        mealPlanDay.HasMany(x => x.Meals)
            .WithOne(x => x.MealPlanDay)
            .HasForeignKey(x => x.MealPlanDayId)
            .OnDelete(DeleteBehavior.Cascade);

        var plannedMeal = modelBuilder.Entity<PlannedMeal>();
        plannedMeal.ToTable("PlannedMeals");
        plannedMeal.HasKey(x => x.Id);
        plannedMeal.Property(x => x.SlotType).HasConversion<string>().HasMaxLength(24);
        plannedMeal.Property(x => x.CandidateCode).HasMaxLength(80).IsRequired();
        plannedMeal.Property(x => x.Title).HasMaxLength(200).IsRequired();
        plannedMeal.Property(x => x.Description).HasMaxLength(600).IsRequired();
        plannedMeal.Property(x => x.Cuisine).HasMaxLength(80).IsRequired();
        plannedMeal.Property(x => x.ServingSize).HasPrecision(8, 2);
        plannedMeal.Property(x => x.ServingUnit).HasMaxLength(64).IsRequired();
        plannedMeal.Property(x => x.ProteinGrams).HasPrecision(8, 2);
        plannedMeal.Property(x => x.CarbsGrams).HasPrecision(8, 2);
        plannedMeal.Property(x => x.FatGrams).HasPrecision(8, 2);
        plannedMeal.Property(x => x.FiberGrams).HasPrecision(8, 2);
        plannedMeal.Property(x => x.AllergensCsv).HasMaxLength(500).IsRequired();
        plannedMeal.Property(x => x.IngredientsCsv).HasMaxLength(800).IsRequired();
        plannedMeal.Property(x => x.DeterministicScore).HasPrecision(8, 2);
        plannedMeal.Property(x => x.ReasonCodesCsv).HasMaxLength(500).IsRequired();
        plannedMeal.Property(x => x.ExplanationSummary).HasMaxLength(1000).IsRequired();
        plannedMeal.HasIndex(x => new { x.MealPlanDayId, x.SlotOrder }).IsUnique();
        plannedMeal.HasIndex(x => new { x.UserId, x.MealPlanId });
    }

    private static void ConfigureTrackingModel(ModelBuilder modelBuilder)
    {
        var mealLog = modelBuilder.Entity<MealLog>();
        mealLog.ToTable("MealLogs");
        mealLog.HasKey(x => x.Id);
        mealLog.Property(x => x.TimeZone).HasMaxLength(100).IsRequired();
        mealLog.Property(x => x.MealType).HasConversion<string>().HasMaxLength(24);
        mealLog.Property(x => x.Title).HasMaxLength(200).IsRequired();
        mealLog.Property(x => x.Notes).HasMaxLength(1000);
        mealLog.Property(x => x.TotalProteinGrams).HasPrecision(8, 2);
        mealLog.Property(x => x.TotalCarbsGrams).HasPrecision(8, 2);
        mealLog.Property(x => x.TotalFatGrams).HasPrecision(8, 2);
        mealLog.Property(x => x.TotalFiberGrams).HasPrecision(8, 2);
        mealLog.HasIndex(x => new { x.UserId, x.LocalDate, x.IsDeleted });
        mealLog.HasIndex(x => new { x.UserId, x.EatenAt });
        mealLog.HasMany(x => x.Items)
            .WithOne(x => x.MealLog)
            .HasForeignKey(x => x.MealLogId)
            .OnDelete(DeleteBehavior.Cascade);

        var mealLogItem = modelBuilder.Entity<MealLogItem>();
        mealLogItem.ToTable("MealLogItems");
        mealLogItem.HasKey(x => x.Id);
        mealLogItem.Property(x => x.FoodCode).HasMaxLength(80);
        mealLogItem.Property(x => x.FoodName).HasMaxLength(200).IsRequired();
        mealLogItem.Property(x => x.ServingAmount).HasPrecision(8, 2);
        mealLogItem.Property(x => x.ServingUnit).HasMaxLength(64).IsRequired();
        mealLogItem.Property(x => x.ProteinGrams).HasPrecision(8, 2);
        mealLogItem.Property(x => x.CarbsGrams).HasPrecision(8, 2);
        mealLogItem.Property(x => x.FatGrams).HasPrecision(8, 2);
        mealLogItem.Property(x => x.FiberGrams).HasPrecision(8, 2);

        var eatingWindow = modelBuilder.Entity<EatingWindowSnapshot>();
        eatingWindow.ToTable("EatingWindowSnapshots");
        eatingWindow.HasKey(x => x.Id);
        eatingWindow.Property(x => x.TimeZone).HasMaxLength(100).IsRequired();
        eatingWindow.Property(x => x.StatusCode).HasMaxLength(64).IsRequired();
        eatingWindow.Property(x => x.SummaryMessage).HasMaxLength(600).IsRequired();
        eatingWindow.HasIndex(x => new { x.UserId, x.LocalDate }).IsUnique();
    }

    private static void ConfigureRecommendationModel(ModelBuilder modelBuilder)
    {
        var feedback = modelBuilder.Entity<RecommendationFeedback>();
        feedback.ToTable("RecommendationFeedbacks");
        feedback.HasKey(x => x.Id);
        feedback.Property(x => x.CandidateCode).HasMaxLength(80).IsRequired();
        feedback.Property(x => x.CandidateTitle).HasMaxLength(200).IsRequired();
        feedback.Property(x => x.FeedbackType).HasConversion<string>().HasMaxLength(24);
        feedback.Property(x => x.ReasonCode).HasMaxLength(80).IsRequired();
        feedback.Property(x => x.Notes).HasMaxLength(600);
        feedback.Property(x => x.RuleSetVersion).HasMaxLength(64).IsRequired();
        feedback.Property(x => x.WeightProfileVersion).HasMaxLength(64).IsRequired();
        feedback.HasIndex(x => new { x.UserId, x.CandidateCode, x.CreatedAt });

        var replacementHistory = modelBuilder.Entity<MealReplacementHistory>();
        replacementHistory.ToTable("MealReplacementHistories");
        replacementHistory.HasKey(x => x.Id);
        replacementHistory.Property(x => x.SlotType).HasConversion<string>().HasMaxLength(24);
        replacementHistory.Property(x => x.PreviousCandidateCode).HasMaxLength(80).IsRequired();
        replacementHistory.Property(x => x.PreviousTitle).HasMaxLength(200).IsRequired();
        replacementHistory.Property(x => x.PreviousScore).HasPrecision(8, 2);
        replacementHistory.Property(x => x.ReplacementCandidateCode).HasMaxLength(80).IsRequired();
        replacementHistory.Property(x => x.ReplacementTitle).HasMaxLength(200).IsRequired();
        replacementHistory.Property(x => x.ReplacementScore).HasPrecision(8, 2);
        replacementHistory.Property(x => x.ReplacementReasonCode).HasMaxLength(80).IsRequired();
        replacementHistory.Property(x => x.ReasonSummary).HasMaxLength(1000).IsRequired();
        replacementHistory.Property(x => x.AppliedAllergiesCsv).HasMaxLength(1000).IsRequired();
        replacementHistory.Property(x => x.AppliedExclusionsCsv).HasMaxLength(1000).IsRequired();
        replacementHistory.Property(x => x.RuleSetVersion).HasMaxLength(64).IsRequired();
        replacementHistory.Property(x => x.WeightProfileVersion).HasMaxLength(64).IsRequired();
        replacementHistory.HasIndex(x => new { x.UserId, x.MealPlanId, x.ReplacedAt });
        replacementHistory.HasIndex(x => new { x.PlannedMealId, x.ReplacedAt });

        var foodPreference = modelBuilder.Entity<UserFoodPreference>();
        foodPreference.ToTable("UserFoodPreferences");
        foodPreference.HasKey(x => x.Id);
        foodPreference.Property(x => x.FoodToken).HasMaxLength(120).IsRequired();
        foodPreference.Property(x => x.DisplayLabel).HasMaxLength(200).IsRequired();
        foodPreference.Property(x => x.PreferenceKind).HasConversion<string>().HasMaxLength(32);
        foodPreference.Property(x => x.Source).HasMaxLength(64).IsRequired();
        foodPreference.Property(x => x.Notes).HasMaxLength(500);
        foodPreference.HasIndex(x => new { x.UserId, x.FoodToken }).IsUnique();
    }
}
