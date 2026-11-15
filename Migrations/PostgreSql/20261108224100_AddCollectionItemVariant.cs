using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KeyPulse.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class AddCollectionItemVariant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CollectionItems_PartKind_CatalogEntryId",
                table: "CollectionItems");

            migrationBuilder.AddColumn<string>(
                name: "Variant",
                table: "CollectionItems",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionItems_PartKind_CatalogEntryId_Variant",
                table: "CollectionItems",
                columns: new[] { "PartKind", "CatalogEntryId", "Variant" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_CollectionItems_Variant",
                table: "CollectionItems",
                sql: "\"CatalogEntryId\" IS NOT NULL OR \"Variant\" = ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CollectionItems_PartKind_CatalogEntryId_Variant",
                table: "CollectionItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CollectionItems_Variant",
                table: "CollectionItems");

            migrationBuilder.DropColumn(
                name: "Variant",
                table: "CollectionItems");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionItems_PartKind_CatalogEntryId",
                table: "CollectionItems",
                columns: new[] { "PartKind", "CatalogEntryId" },
                unique: true);
        }
    }
}
