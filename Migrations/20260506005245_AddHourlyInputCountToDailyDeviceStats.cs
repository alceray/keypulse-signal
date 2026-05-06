using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KeyPulse.Migrations
{
    /// <inheritdoc />
    public partial class AddHourlyInputCountToDailyDeviceStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DistinctActiveHours",
                table: "DailyDeviceStats");

            migrationBuilder.DropColumn(
                name: "PeakInputHour",
                table: "DailyDeviceStats");

            migrationBuilder.AddColumn<string>(
                name: "HourlyInputCount",
                table: "DailyDeviceStats",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HourlyInputCount",
                table: "DailyDeviceStats");

            migrationBuilder.AddColumn<int>(
                name: "DistinctActiveHours",
                table: "DailyDeviceStats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PeakInputHour",
                table: "DailyDeviceStats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
