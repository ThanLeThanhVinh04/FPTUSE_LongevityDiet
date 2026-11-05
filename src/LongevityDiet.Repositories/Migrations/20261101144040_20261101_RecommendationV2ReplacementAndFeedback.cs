using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LongevityDiet.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class _20261101_RecommendationV2ReplacementAndFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MealReplacementHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MealPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MealPlanDayId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlannedMealId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DayNumber = table.Column<int>(type: "int", nullable: false),
                    SlotType = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    PreviousCandidateCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    PreviousTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PreviousScore = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    ReplacementCandidateCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ReplacementTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReplacementScore = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    ReplacementReasonCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ReasonSummary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    AppliedAllergiesCsv = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    AppliedExclusionsCsv = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RuleSetVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    WeightProfileVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ReplacedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealReplacementHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecommendationFeedbacks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CandidateCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    CandidateTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PlannedMealId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FeedbackType = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    RuleSetVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    WeightProfileVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecommendationFeedbacks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserFoodPreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FoodToken = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    DisplayLabel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PreferenceKind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserFoodPreferences", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MealReplacementHistories_PlannedMealId_ReplacedAt",
                table: "MealReplacementHistories",
                columns: new[] { "PlannedMealId", "ReplacedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MealReplacementHistories_UserId_MealPlanId_ReplacedAt",
                table: "MealReplacementHistories",
                columns: new[] { "UserId", "MealPlanId", "ReplacedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RecommendationFeedbacks_UserId_CandidateCode_CreatedAt",
                table: "RecommendationFeedbacks",
                columns: new[] { "UserId", "CandidateCode", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserFoodPreferences_UserId_FoodToken",
                table: "UserFoodPreferences",
                columns: new[] { "UserId", "FoodToken" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MealReplacementHistories");

            migrationBuilder.DropTable(
                name: "RecommendationFeedbacks");

            migrationBuilder.DropTable(
                name: "UserFoodPreferences");
        }
    }
}
