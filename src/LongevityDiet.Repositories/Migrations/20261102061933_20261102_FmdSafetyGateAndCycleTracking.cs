using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LongevityDiet.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class _20261102_FmdSafetyGateAndCycleTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FmdCycles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CycleNumber = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TargetDays = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    StoppedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    StopReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FmdCycles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FmdEducationAcknowledgements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EducationVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AcknowledgedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SourceCitation = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    DisclaimerAccepted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FmdEducationAcknowledgements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FmdSafetyAssessments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssessmentVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    EvaluatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResultStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    HasHighRiskFlag = table.Column<bool>(type: "bit", nullable: false),
                    TriggeredRiskCodesCsv = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RecommendationsSummary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    AnswersJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FmdSafetyAssessments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FmdCycleDays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FmdCycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DayNumber = table.Column<int>(type: "int", nullable: false),
                    PlanDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TargetCaloriesKcal = table.Column<int>(type: "int", nullable: false),
                    EnergyLevel = table.Column<int>(type: "int", nullable: true),
                    HungerLevel = table.Column<int>(type: "int", nullable: true),
                    WaterIntakeLiters = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    Symptoms = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DailyNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    LoggedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FmdCycleDays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FmdCycleDays_FmdCycles_FmdCycleId",
                        column: x => x.FmdCycleId,
                        principalTable: "FmdCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FmdCycleDays_FmdCycleId_DayNumber",
                table: "FmdCycleDays",
                columns: new[] { "FmdCycleId", "DayNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FmdCycles_UserId_Status",
                table: "FmdCycles",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_FmdEducationAcknowledgements_UserId_EducationVersion",
                table: "FmdEducationAcknowledgements",
                columns: new[] { "UserId", "EducationVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_FmdSafetyAssessments_UserId_EvaluatedAt",
                table: "FmdSafetyAssessments",
                columns: new[] { "UserId", "EvaluatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FmdCycleDays");

            migrationBuilder.DropTable(
                name: "FmdEducationAcknowledgements");

            migrationBuilder.DropTable(
                name: "FmdSafetyAssessments");

            migrationBuilder.DropTable(
                name: "FmdCycles");
        }
    }
}
