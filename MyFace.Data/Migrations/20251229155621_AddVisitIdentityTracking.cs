using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyFace.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVisitIdentityTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Path",
                table: "PageVisits",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "EventType",
                table: "PageVisits",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "page-load");

            migrationBuilder.AddColumn<string>(
                name: "SessionFingerprint",
                table: "PageVisits",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "PageVisits",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UsernameSnapshot",
                table: "PageVisits",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PageVisits_SessionFingerprint",
                table: "PageVisits",
                column: "SessionFingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_PageVisits_VisitedAt",
                table: "PageVisits",
                column: "VisitedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PageVisits_SessionFingerprint",
                table: "PageVisits");

            migrationBuilder.DropIndex(
                name: "IX_PageVisits_VisitedAt",
                table: "PageVisits");

            migrationBuilder.DropColumn(
                name: "EventType",
                table: "PageVisits");

            migrationBuilder.DropColumn(
                name: "SessionFingerprint",
                table: "PageVisits");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "PageVisits");

            migrationBuilder.DropColumn(
                name: "UsernameSnapshot",
                table: "PageVisits");

            migrationBuilder.AlterColumn<string>(
                name: "Path",
                table: "PageVisits",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);
        }
    }
}
