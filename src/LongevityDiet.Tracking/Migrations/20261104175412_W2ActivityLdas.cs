using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LongevityDiet.Tracking.Migrations
{
    /// <inheritdoc />
    public partial class W2ActivityLdas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivityGoals",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WeeklyMinutesTarget = table.Column<int>(type: "int", nullable: false),
                    WeeklyStrengthSessionsTarget = table.Column<int>(type: "int", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityGoals", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "ActivityLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(800)", maxLength: 800, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdherenceScores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Version = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Score = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    CoveragePercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    IsPartial = table.Column<bool>(type: "bit", nullable: false),
                    MissingDataJson = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    CalculatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdherenceScores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScoreConfigurationVersions",
                columns: table => new
                {
                    Version = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Published = table.Column<bool>(type: "bit", nullable: false),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ActivityWeight = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    MealWeight = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    EatingWindowWeight = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScoreConfigurationVersions", x => x.Version);
                });

            migrationBuilder.CreateTable(
                name: "TrackingScoreSignals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DimensionCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Percent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    SourceEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackingScoreSignals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScoreDimensions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdherenceScoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    RawScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    WeightedScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    ReasonCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScoreDimensions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScoreDimensions_AdherenceScores_AdherenceScoreId",
                        column: x => x.AdherenceScoreId,
                        principalTable: "AdherenceScores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "ScoreConfigurationVersions",
                columns: new[] { "Version", "ActivityWeight", "EatingWindowWeight", "MealWeight", "Published", "PublishedAtUtc", "SourceReference" },
                values: new object[] { "ldas-v1", 0.35m, 0.20m, 0.45m, true, new DateTimeOffset(new DateTime(2026, 11, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "project-scoring-policy-v1; educational, not clinically validated" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_UserId_IsDeleted_StartedAtUtc",
                table: "ActivityLogs",
                columns: new[] { "UserId", "IsDeleted", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_UserId_StartedAtUtc",
                table: "ActivityLogs",
                columns: new[] { "UserId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AdherenceScores_UserId_LocalDate_Version",
                table: "AdherenceScores",
                columns: new[] { "UserId", "LocalDate", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScoreDimensions_AdherenceScoreId_Code",
                table: "ScoreDimensions",
                columns: new[] { "AdherenceScoreId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrackingScoreSignals_SourceEventId",
                table: "TrackingScoreSignals",
                column: "SourceEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrackingScoreSignals_UserId_LocalDate_DimensionCode",
                table: "TrackingScoreSignals",
                columns: new[] { "UserId", "LocalDate", "DimensionCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityGoals");

            migrationBuilder.DropTable(
                name: "ActivityLogs");

            migrationBuilder.DropTable(
                name: "ScoreConfigurationVersions");

            migrationBuilder.DropTable(
                name: "ScoreDimensions");

            migrationBuilder.DropTable(
                name: "TrackingScoreSignals");

            migrationBuilder.DropTable(
                name: "AdherenceScores");
        }
    }
}
