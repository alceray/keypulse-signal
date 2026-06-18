using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KeyPulse.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLongestSessionSeconds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LongestSessionSeconds",
                table: "DailyDeviceStats");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "LongestSessionSeconds",
                table: "DailyDeviceStats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);
        }
    }
}
