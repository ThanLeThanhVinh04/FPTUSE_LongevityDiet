using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LongevityDiet.Tracking.Migrations
{
    /// <inheritdoc />
    public partial class W2ActivityEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TrackingOutbox",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishAttempts = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackingOutbox", x => x.EventId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrackingOutbox_PublishedAtUtc_CreatedAtUtc",
                table: "TrackingOutbox",
                columns: new[] { "PublishedAtUtc", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrackingOutbox");
        }
    }
}
