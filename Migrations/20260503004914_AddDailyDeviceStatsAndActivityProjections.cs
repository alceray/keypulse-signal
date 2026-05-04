using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KeyPulse.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyDeviceStatsAndActivityProjections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivityProjections",
                columns: table => new
                {
                    ActivityProjectionId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DeviceId = table.Column<string>(type: "TEXT", nullable: false),
                    Minute = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ProjectedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityProjections", x => x.ActivityProjectionId);
                });

            migrationBuilder.CreateTable(
                name: "DailyDeviceStats",
                columns: table => new
                {
                    DailyDeviceStatId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Day = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<string>(type: "TEXT", nullable: false),
                    SessionCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ConnectionDuration = table.Column<long>(type: "INTEGER", nullable: false),
                    LongestSessionDuration = table.Column<long>(type: "INTEGER", nullable: false),
                    Keystrokes = table.Column<long>(type: "INTEGER", nullable: false),
                    MouseClicks = table.Column<long>(type: "INTEGER", nullable: false),
                    MouseMovementSeconds = table.Column<long>(type: "INTEGER", nullable: false),
                    DistinctActiveHours = table.Column<int>(type: "INTEGER", nullable: false),
                    ActiveMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    PeakInputHour = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyDeviceStats", x => x.DailyDeviceStatId);
                });

            migrationBuilder.CreateIndex(
                name: "Idx_ActivityProjections_DeviceIdMinute",
                table: "ActivityProjections",
                columns: new[] { "DeviceId", "Minute" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "Idx_DailyDeviceStats_Day",
                table: "DailyDeviceStats",
                column: "Day");

            migrationBuilder.CreateIndex(
                name: "Idx_DailyDeviceStats_DayDeviceId",
                table: "DailyDeviceStats",
                columns: new[] { "Day", "DeviceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "Idx_DailyDeviceStats_DeviceIdDay",
                table: "DailyDeviceStats",
                columns: new[] { "DeviceId", "Day" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityProjections");

            migrationBuilder.DropTable(
                name: "DailyDeviceStats");
        }
    }
}
