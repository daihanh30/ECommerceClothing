using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerceClothing.Migrations
{
    /// <inheritdoc />
    public partial class AddMapboxToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MapboxToken",
                table: "ShopSettings",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MapboxToken",
                table: "ShopSettings");
        }
    }
}
