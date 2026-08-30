using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KeyPulse.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class PostgreSqlInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivityProjections",
                columns: table => new
                {
                    ActivityProjectionId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeviceId = table.Column<string>(type: "text", nullable: false),
                    Minute = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProjectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityProjections", x => x.ActivityProjectionId);
                });

            migrationBuilder.CreateTable(
                name: "ActivitySnapshots",
                columns: table => new
                {
                    ActivitySnapshotId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeviceId = table.Column<string>(type: "text", nullable: false),
                    Minute = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Keystrokes = table.Column<int>(type: "integer", nullable: false),
                    MouseClicks = table.Column<int>(type: "integer", nullable: false),
                    MouseMovementSeconds = table.Column<byte>(type: "smallint", nullable: false),
                    ActiveSeconds = table.Column<byte>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivitySnapshots", x => x.ActivitySnapshotId);
                });

            migrationBuilder.CreateTable(
                name: "DailyDeviceStats",
                columns: table => new
                {
                    DailyDeviceStatId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    DeviceId = table.Column<string>(type: "text", nullable: false),
                    SessionCount = table.Column<int>(type: "integer", nullable: false),
                    ConnectionSeconds = table.Column<long>(type: "bigint", nullable: false),
                    Keystrokes = table.Column<long>(type: "bigint", nullable: false),
                    MouseClicks = table.Column<long>(type: "bigint", nullable: false),
                    MouseMovementSeconds = table.Column<long>(type: "bigint", nullable: false),
                    ActiveSeconds = table.Column<long>(type: "bigint", nullable: false),
                    HourlyInputCount = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyDeviceStats", x => x.DailyDeviceStatId);
                });

            migrationBuilder.CreateTable(
                name: "DeviceEvents",
                columns: table => new
                {
                    DeviceEventId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EventTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    DeviceId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceEvents", x => x.DeviceEventId);
                });

            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    DeviceId = table.Column<string>(type: "text", nullable: false),
                    DeviceType = table.Column<string>(type: "text", nullable: false),
                    DeviceName = table.Column<string>(type: "text", nullable: false),
                    TotalConnectionSeconds = table.Column<long>(type: "bigint", nullable: false),
                    SessionStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastConnectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsHiddenFromDisplay = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TotalInputCount = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    DaysConnected = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.DeviceId);
                });

            migrationBuilder.CreateIndex(
                name: "Idx_ActivityProjections_DeviceIdMinute",
                table: "ActivityProjections",
                columns: new[] { "DeviceId", "Minute" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "Idx_ActivitySnapshots_DeviceIdMinute",
                table: "ActivitySnapshots",
                columns: new[] { "DeviceId", "Minute" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "Idx_ActivitySnapshots_Minute",
                table: "ActivitySnapshots",
                column: "Minute");

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

            migrationBuilder.CreateIndex(
                name: "Idx_DeviceEvents_DeviceIdEventTime",
                table: "DeviceEvents",
                columns: new[] { "DeviceId", "EventTime" });

            migrationBuilder.CreateIndex(
                name: "Idx_DeviceEvents_EventTime",
                table: "DeviceEvents",
                column: "EventTime");

            migrationBuilder.CreateIndex(
                name: "Idx_DeviceEvents_Unique",
                table: "DeviceEvents",
                columns: new[] { "DeviceId", "EventTime", "EventType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "Idx_Devices_DeviceId",
                table: "Devices",
                column: "DeviceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityProjections");

            migrationBuilder.DropTable(
                name: "ActivitySnapshots");

            migrationBuilder.DropTable(
                name: "DailyDeviceStats");

            migrationBuilder.DropTable(
                name: "DeviceEvents");

            migrationBuilder.DropTable(
                name: "Devices");
        }
    }
}
