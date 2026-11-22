using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KeyPulse.Migrations.PostgreSql
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
                    DeviceBuildDayId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DayKey = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<string>(type: "text", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    BuildsSchemaVersion = table.Column<int>(type: "integer", nullable: false),
                    BuildsJson = table.Column<string>(type: "text", nullable: false)
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
