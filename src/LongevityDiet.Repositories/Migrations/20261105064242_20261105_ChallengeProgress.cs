using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LongevityDiet.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class _20261105_ChallengeProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChallengeTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DurationDays = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChallengeTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DailyWellnessSummaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    LoggedMealCount = table.Column<int>(type: "int", nullable: false),
                    TotalCaloriesKcal = table.Column<int>(type: "int", nullable: false),
                    EatingWindowWithinTarget = table.Column<bool>(type: "bit", nullable: true),
                    ChallengeDayCompleted = table.Column<bool>(type: "bit", nullable: false),
                    ChallengeCurrentStreak = table.Column<int>(type: "int", nullable: false),
                    LoggingConsistencyScore = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    DataAvailability = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyWellnessSummaries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProgressEventReceipts",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgressEventReceipts", x => x.EventId);
                });

            migrationBuilder.CreateTable(
                name: "ChallengeDayTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChallengeTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DayNumber = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Guidance = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChallengeDayTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChallengeDayTemplates_ChallengeTemplates_ChallengeTemplateId",
                        column: x => x.ChallengeTemplateId,
                        principalTable: "ChallengeTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserChallenges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChallengeTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TimeZone = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserChallenges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserChallenges_ChallengeTemplates_ChallengeTemplateId",
                        column: x => x.ChallengeTemplateId,
                        principalTable: "ChallengeTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserChallengeDays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserChallengeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DayNumber = table.Column<int>(type: "int", nullable: false),
                    LocalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserChallengeDays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserChallengeDays_UserChallenges_UserChallengeId",
                        column: x => x.UserChallengeId,
                        principalTable: "UserChallenges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChallengeDayTemplates_ChallengeTemplateId_DayNumber",
                table: "ChallengeDayTemplates",
                columns: new[] { "ChallengeTemplateId", "DayNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChallengeTemplates_Code",
                table: "ChallengeTemplates",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyWellnessSummaries_UserId_LocalDate",
                table: "DailyWellnessSummaries",
                columns: new[] { "UserId", "LocalDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProgressEventReceipts_ProcessedAt",
                table: "ProgressEventReceipts",
                column: "ProcessedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserChallengeDays_UserChallengeId_DayNumber",
                table: "UserChallengeDays",
                columns: new[] { "UserChallengeId", "DayNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserChallengeDays_UserChallengeId_LocalDate",
                table: "UserChallengeDays",
                columns: new[] { "UserChallengeId", "LocalDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserChallenges_ChallengeTemplateId",
                table: "UserChallenges",
                column: "ChallengeTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_UserChallenges_UserId_StartDate",
                table: "UserChallenges",
                columns: new[] { "UserId", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_UserChallenges_UserId_Status",
                table: "UserChallenges",
                columns: new[] { "UserId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChallengeDayTemplates");

            migrationBuilder.DropTable(
                name: "DailyWellnessSummaries");

            migrationBuilder.DropTable(
                name: "ProgressEventReceipts");

            migrationBuilder.DropTable(
                name: "UserChallengeDays");

            migrationBuilder.DropTable(
                name: "UserChallenges");

            migrationBuilder.DropTable(
                name: "ChallengeTemplates");
        }
    }
}
