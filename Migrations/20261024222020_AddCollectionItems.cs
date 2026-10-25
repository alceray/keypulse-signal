using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KeyPulse.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectionItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CollectionItems",
                columns: table => new
                {
                    CollectionItemId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ItemKey = table.Column<Guid>(type: "TEXT", nullable: false),
                    PartKind = table.Column<string>(type: "TEXT", nullable: false),
                    CatalogEntryId = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Manufacturer = table.Column<string>(type: "TEXT", nullable: true),
                    Brand = table.Column<string>(type: "TEXT", nullable: true),
                    Designer = table.Column<string>(type: "TEXT", nullable: true),
                    DetailsSchemaVersion = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    DetailsJson = table.Column<string>(type: "TEXT", nullable: false),
                    QuantityUnit = table.Column<string>(type: "TEXT", nullable: false),
                    Revision = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 1L),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    AddedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionItems", x => x.CollectionItemId);
                    table.CheckConstraint("CK_CollectionItems_CatalogEntryId", "\"CatalogEntryId\" IS NULL OR length(trim(\"CatalogEntryId\")) > 0");
                    table.CheckConstraint("CK_CollectionItems_Quantity", "\"Quantity\" >= 1 AND \"Quantity\" <= 1000000");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionItems_ItemKey",
                table: "CollectionItems",
                column: "ItemKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollectionItems_PartKind_CatalogEntryId",
                table: "CollectionItems",
                columns: new[] { "PartKind", "CatalogEntryId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CollectionItems");
        }
    }
}
