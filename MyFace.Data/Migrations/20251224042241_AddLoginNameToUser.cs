using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyFace.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLoginNameToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add LoginName column (allow null initially)
            migrationBuilder.AddColumn<string>(
                name: "LoginName",
                table: "Users",
                type: "text",
                nullable: true,
                defaultValue: null);

            // Migrate existing data: copy Username to LoginName for existing users
            migrationBuilder.Sql(
                "UPDATE \"Users\" SET \"LoginName\" = \"Username\" WHERE \"LoginName\" IS NULL;");

            // Now make LoginName not nullable
            migrationBuilder.AlterColumn<string>(
                name: "LoginName",
                table: "Users",
                type: "text",
                nullable: false);

            // Username can now be empty (will be set by user after registration)
            // Existing users keep their Username as both login and display name
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LoginName",
                table: "Users");
        }
    }
}
