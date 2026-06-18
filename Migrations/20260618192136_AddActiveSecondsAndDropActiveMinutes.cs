using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KeyPulse.Migrations
{
    /// <inheritdoc />
    public partial class AddActiveSecondsAndDropActiveMinutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ActiveSeconds",
                table: "DailyDeviceStats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<byte>(
                name: "ActiveSeconds",
                table: "ActivitySnapshots",
                type: "INTEGER",
                nullable: false,
                defaultValue: (byte)0);

            // Seed the new per-second column from the old minute count (clamped to connected time) before
            // dropping it, so existing days keep their active time instead of resetting to zero.
            migrationBuilder.Sql(
                "UPDATE DailyDeviceStats SET ActiveSeconds = MIN(ActiveMinutes * 60, ConnectionSeconds);"
            );

            migrationBuilder.DropColumn(name: "ActiveMinutes", table: "DailyDeviceStats");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActiveMinutes",
                table: "DailyDeviceStats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.DropColumn(name: "ActiveSeconds", table: "DailyDeviceStats");

            migrationBuilder.DropColumn(name: "ActiveSeconds", table: "ActivitySnapshots");
        }
    }
}
