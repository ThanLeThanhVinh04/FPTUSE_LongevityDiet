using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LongevityDiet.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class W3ReminderNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IdentityInbox",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdentityInbox", x => x.EventId);
                });

            migrationBuilder.CreateTable(
                name: "IdentityOutbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(96)", maxLength: 96, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishAttempts = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdentityOutbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReminderPreferences",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OptedIn = table.Column<bool>(type: "bit", nullable: false),
                    MealLoggingEnabled = table.Column<bool>(type: "bit", nullable: false),
                    ActivityEnabled = table.Column<bool>(type: "bit", nullable: false),
                    ChallengeEnabled = table.Column<bool>(type: "bit", nullable: false),
                    WeeklySummaryEnabled = table.Column<bool>(type: "bit", nullable: false),
                    MealTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    ActivityTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    ChallengeTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    WeeklySummaryTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    QuietStart = table.Column<TimeOnly>(type: "time", nullable: false),
                    QuietEnd = table.Column<TimeOnly>(type: "time", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReminderPreferences", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_ReminderPreferences_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DeepLink = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    DeduplicationKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserNotifications_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IdentityOutbox_PublishedAtUtc_CreatedAtUtc",
                table: "IdentityOutbox",
                columns: new[] { "PublishedAtUtc", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_UserId_CreatedAtUtc",
                table: "UserNotifications",
                columns: new[] { "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_UserId_DeduplicationKey",
                table: "UserNotifications",
                columns: new[] { "UserId", "DeduplicationKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdentityInbox");

            migrationBuilder.DropTable(
                name: "IdentityOutbox");

            migrationBuilder.DropTable(
                name: "ReminderPreferences");

            migrationBuilder.DropTable(
                name: "UserNotifications");
        }
    }
}
