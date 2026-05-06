using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KeyPulse.Migrations
{
    /// <inheritdoc />
    public partial class RenameConnectionDurationsToSeconds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConnectionDuration",
                table: "Devices");

            migrationBuilder.RenameColumn(
                name: "LongestSessionDuration",
                table: "DailyDeviceStats",
                newName: "LongestSessionSeconds");

            migrationBuilder.RenameColumn(
                name: "ConnectionDuration",
                table: "DailyDeviceStats",
                newName: "ConnectionSeconds");

            migrationBuilder.AddColumn<long>(
                name: "TotalConnectionSeconds",
                table: "Devices",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TotalConnectionSeconds",
                table: "Devices");

            migrationBuilder.RenameColumn(
                name: "LongestSessionSeconds",
                table: "DailyDeviceStats",
                newName: "LongestSessionDuration");

            migrationBuilder.RenameColumn(
                name: "ConnectionSeconds",
                table: "DailyDeviceStats",
                newName: "ConnectionDuration");

            migrationBuilder.AddColumn<TimeSpan>(
                name: "ConnectionDuration",
                table: "Devices",
                type: "TEXT",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));
        }
    }
}
