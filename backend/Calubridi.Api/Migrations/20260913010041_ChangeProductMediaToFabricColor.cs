using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Calubridi.Api.Migrations
{
    /// <inheritdoc />
    public partial class ChangeProductMediaToFabricColor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductMedia_Products_ProductId",
                table: "ProductMedia");

            migrationBuilder.RenameColumn(
                name: "ProductId",
                table: "ProductMedia",
                newName: "ProductFabricColorId");

            migrationBuilder.RenameIndex(
                name: "IX_ProductMedia_ProductId",
                table: "ProductMedia",
                newName: "IX_ProductMedia_ProductFabricColorId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductMedia_ProductFabricColors_ProductFabricColorId",
                table: "ProductMedia",
                column: "ProductFabricColorId",
                principalTable: "ProductFabricColors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductMedia_ProductFabricColors_ProductFabricColorId",
                table: "ProductMedia");

            migrationBuilder.RenameColumn(
                name: "ProductFabricColorId",
                table: "ProductMedia",
                newName: "ProductId");

            migrationBuilder.RenameIndex(
                name: "IX_ProductMedia_ProductFabricColorId",
                table: "ProductMedia",
                newName: "IX_ProductMedia_ProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductMedia_Products_ProductId",
                table: "ProductMedia",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
