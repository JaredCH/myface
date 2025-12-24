using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyFace.Data.Migrations
{
    /// <summary>
    /// Adds vendor profile sections and button theming fields to Users.
    /// </summary>
    public partial class AddVendorAndButtonTheme : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ButtonBackgroundColor",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "#0ea5e9");

            migrationBuilder.AddColumn<string>(
                name: "ButtonBorderColor",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "#0ea5e9");

            migrationBuilder.AddColumn<string>(
                name: "ButtonTextColor",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "#ffffff");

            migrationBuilder.AddColumn<string>(
                name: "VendorExternalReferences",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "VendorPayments",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "VendorPolicies",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "VendorShopDescription",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ButtonBackgroundColor",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ButtonBorderColor",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ButtonTextColor",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "VendorExternalReferences",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "VendorPayments",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "VendorPolicies",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "VendorShopDescription",
                table: "Users");
        }
    }
}
