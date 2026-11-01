using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LongevityDiet.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class _20261101_MealPlanningLoggingAndEatingWindow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EatingWindowSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TimeZone = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LoggedMealCount = table.Column<int>(type: "int", nullable: false),
                    CaloricMealCount = table.Column<int>(type: "int", nullable: false),
                    TotalCaloriesKcal = table.Column<int>(type: "int", nullable: false),
                    FirstMealAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastMealAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FirstMealLocalTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    LastMealLocalTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    EatingWindowMinutes = table.Column<int>(type: "int", nullable: true),
                    TargetWindowMinutes = table.Column<int>(type: "int", nullable: false),
                    IsWithinTargetWindow = table.Column<bool>(type: "bit", nullable: false),
                    ConfiguredSleepTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    MinutesBeforeBedtime = table.Column<int>(type: "int", nullable: true),
                    IsLateEatingFlagged = table.Column<bool>(type: "bit", nullable: false),
                    StatusCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SummaryMessage = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    CalculatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EatingWindowSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MealLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlannedMealId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EatenAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LocalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TimeZone = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MealType = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TotalCaloriesKcal = table.Column<int>(type: "int", nullable: false),
                    TotalProteinGrams = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    TotalCarbsGrams = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    TotalFatGrams = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    TotalFiberGrams = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MealPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleSetVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    PreviousPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    MealsPerDay = table.Column<int>(type: "int", nullable: false),
                    TimeZone = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DietaryPattern = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AppliedAllergiesCsv = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    AppliedExclusionsCsv = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ReasonCodesCsv = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PlanExplanation = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserDietaryPreferences",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DietaryPattern = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AllergiesCsv = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ExcludedFoodsCsv = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    PreferredCuisine = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDietaryPreferences", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "MealLogItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MealLogId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FoodCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    FoodName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ServingAmount = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    ServingUnit = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CaloriesKcal = table.Column<int>(type: "int", nullable: false),
                    ProteinGrams = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    CarbsGrams = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    FatGrams = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    FiberGrams = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    IsPlantBased = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealLogItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MealLogItems_MealLogs_MealLogId",
                        column: x => x.MealLogId,
                        principalTable: "MealLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MealPlanDays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MealPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DayNumber = table.Column<int>(type: "int", nullable: false),
                    PlanDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TargetCaloriesKcal = table.Column<int>(type: "int", nullable: false),
                    DailyFocusCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DailyFocusTitle = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealPlanDays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MealPlanDays_MealPlans_MealPlanId",
                        column: x => x.MealPlanId,
                        principalTable: "MealPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlannedMeals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MealPlanDayId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MealPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SlotOrder = table.Column<int>(type: "int", nullable: false),
                    SlotType = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    PlannedTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    CandidateCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    Cuisine = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ServingSize = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    ServingUnit = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CaloriesKcal = table.Column<int>(type: "int", nullable: false),
                    ProteinGrams = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    CarbsGrams = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    FatGrams = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    FiberGrams = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    IsPlantBased = table.Column<bool>(type: "bit", nullable: false),
                    HasLegumes = table.Column<bool>(type: "bit", nullable: false),
                    HasWholeGrains = table.Column<bool>(type: "bit", nullable: false),
                    ContainsFish = table.Column<bool>(type: "bit", nullable: false),
                    AllergensCsv = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IngredientsCsv = table.Column<string>(type: "nvarchar(800)", maxLength: 800, nullable: false),
                    DeterministicScore = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    ReasonCodesCsv = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ExplanationSummary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RegenerationCount = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlannedMeals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlannedMeals_MealPlanDays_MealPlanDayId",
                        column: x => x.MealPlanDayId,
                        principalTable: "MealPlanDays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EatingWindowSnapshots_UserId_LocalDate",
                table: "EatingWindowSnapshots",
                columns: new[] { "UserId", "LocalDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MealLogItems_MealLogId",
                table: "MealLogItems",
                column: "MealLogId");

            migrationBuilder.CreateIndex(
                name: "IX_MealLogs_UserId_EatenAt",
                table: "MealLogs",
                columns: new[] { "UserId", "EatenAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MealLogs_UserId_LocalDate_IsDeleted",
                table: "MealLogs",
                columns: new[] { "UserId", "LocalDate", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_MealPlanDays_MealPlanId_DayNumber",
                table: "MealPlanDays",
                columns: new[] { "MealPlanId", "DayNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MealPlans_UserId_Status_GeneratedAt",
                table: "MealPlans",
                columns: new[] { "UserId", "Status", "GeneratedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PlannedMeals_MealPlanDayId_SlotOrder",
                table: "PlannedMeals",
                columns: new[] { "MealPlanDayId", "SlotOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlannedMeals_UserId_MealPlanId",
                table: "PlannedMeals",
                columns: new[] { "UserId", "MealPlanId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EatingWindowSnapshots");

            migrationBuilder.DropTable(
                name: "MealLogItems");

            migrationBuilder.DropTable(
                name: "PlannedMeals");

            migrationBuilder.DropTable(
                name: "UserDietaryPreferences");

            migrationBuilder.DropTable(
                name: "MealLogs");

            migrationBuilder.DropTable(
                name: "MealPlanDays");

            migrationBuilder.DropTable(
                name: "MealPlans");
        }
    }
}
