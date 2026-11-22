using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KeyPulse.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceBuildDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeviceBuildDays",
                columns: table => new
                {
                    DeviceBuildDayId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DayKey = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<string>(type: "TEXT", nullable: false),
                    Day = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Revision = table.Column<long>(type: "INTEGER", nullable: false),
                    BuildsSchemaVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    BuildsJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceBuildDays", x => x.DeviceBuildDayId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceBuildDays_Day",
                table: "DeviceBuildDays",
                column: "Day");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceBuildDays_DayKey",
                table: "DeviceBuildDays",
                column: "DayKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceBuildDays_DeviceId_Day",
                table: "DeviceBuildDays",
                columns: new[] { "DeviceId", "Day" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceBuildDays");
        }
    }
}
