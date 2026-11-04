using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LongevityDiet.Worker.Reminders.Migrations
{
    /// <inheritdoc />
    public partial class W3ReminderWorker : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReminderDispatchRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeduplicationKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReminderDispatchRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReminderSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TimeZone = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LocalTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    QuietStart = table.Column<TimeOnly>(type: "time", nullable: false),
                    QuietEnd = table.Column<TimeOnly>(type: "time", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    PreferenceVersion = table.Column<int>(type: "int", nullable: false),
                    NextDueUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReminderSchedules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkerOutbox",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishAttempts = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkerOutbox", x => x.EventId);
                });

            migrationBuilder.CreateTable(
                name: "WorkerProcessedEvents",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkerProcessedEvents", x => x.EventId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReminderDispatchRecords_DeduplicationKey",
                table: "ReminderDispatchRecords",
                column: "DeduplicationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReminderSchedules_Enabled_NextDueUtc",
                table: "ReminderSchedules",
                columns: new[] { "Enabled", "NextDueUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ReminderSchedules_UserId_Category",
                table: "ReminderSchedules",
                columns: new[] { "UserId", "Category" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkerOutbox_PublishedAtUtc_CreatedAtUtc",
                table: "WorkerOutbox",
                columns: new[] { "PublishedAtUtc", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReminderDispatchRecords");

            migrationBuilder.DropTable(
                name: "ReminderSchedules");

            migrationBuilder.DropTable(
                name: "WorkerOutbox");

            migrationBuilder.DropTable(
                name: "WorkerProcessedEvents");
        }
    }
}
