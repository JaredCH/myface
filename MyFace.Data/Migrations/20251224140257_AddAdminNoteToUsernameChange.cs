using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyFace.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminNoteToUsernameChange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdminNote",
                table: "UsernameChangeLogs",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdminNote",
                table: "UsernameChangeLogs");
        }
    }
}
