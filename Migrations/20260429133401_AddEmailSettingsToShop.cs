using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerceClothing.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailSettingsToShop : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            //migrationBuilder.DropColumn(
            //    name: "ContactEmail",
            //    table: "ShopSettings");

            //migrationBuilder.DropColumn(
            //    name: "PhoneNumber",
            //    table: "ShopSettings");

            migrationBuilder.AddColumn<string>(
                name: "SenderEmail",
                table: "ShopSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SenderName",
                table: "ShopSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SenderPassword",
                table: "ShopSettings",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SenderEmail",
                table: "ShopSettings");

            migrationBuilder.DropColumn(
                name: "SenderName",
                table: "ShopSettings");

            migrationBuilder.DropColumn(
                name: "SenderPassword",
                table: "ShopSettings");

            migrationBuilder.AddColumn<string>(
                name: "ContactEmail",
                table: "ShopSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                table: "ShopSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
