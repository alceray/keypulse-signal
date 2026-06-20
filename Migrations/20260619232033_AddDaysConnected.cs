using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KeyPulse.Migrations
{
    /// <inheritdoc />
    public partial class AddDaysConnected : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DaysConnected",
                table: "Devices",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Seed from existing daily aggregates: one row per (day, device), counted where the device
            // had connection time. The startup snapshot rebuild recomputes this, but seed it so the value
            // is correct on the very first read.
            migrationBuilder.Sql(
                @"UPDATE Devices SET DaysConnected = (
                    SELECT COUNT(*) FROM DailyDeviceStats
                    WHERE DailyDeviceStats.DeviceId = Devices.DeviceId AND DailyDeviceStats.ConnectionSeconds > 0
                );"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DaysConnected",
                table: "Devices");
        }
    }
}
