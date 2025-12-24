using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyFace.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnhanceProfileCustomization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccentColor",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BackgroundColor",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BorderColor",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CustomCSS",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "FontSize",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ProfileLayout",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccentColor",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "BackgroundColor",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "BorderColor",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CustomCSS",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "FontSize",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ProfileLayout",
                table: "Users");
        }
    }
}
