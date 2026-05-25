using KeyPulse.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KeyPulse.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260524000000_AddDeviceDisplayVisibility")]
    public partial class AddDeviceDisplayVisibility : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsHiddenFromDisplay",
                table: "Devices",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "IsHiddenFromDisplay", table: "Devices");
        }
    }
}
