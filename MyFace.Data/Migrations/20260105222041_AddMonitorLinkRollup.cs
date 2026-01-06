using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyFace.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMonitorLinkRollup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CanonicalName",
                table: "OnionStatuses",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsMirror",
                table: "OnionStatuses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MirrorPriority",
                table: "OnionStatuses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedKey",
                table: "OnionStatuses",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParentId",
                table: "OnionStatuses",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnionStatuses_NormalizedKey",
                table: "OnionStatuses",
                column: "NormalizedKey");

            migrationBuilder.CreateIndex(
                name: "IX_OnionStatuses_ParentId",
                table: "OnionStatuses",
                column: "ParentId");

            migrationBuilder.AddForeignKey(
                name: "FK_OnionStatuses_OnionStatuses_ParentId",
                table: "OnionStatuses",
                column: "ParentId",
                principalTable: "OnionStatuses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OnionStatuses_OnionStatuses_ParentId",
                table: "OnionStatuses");

            migrationBuilder.DropIndex(
                name: "IX_OnionStatuses_NormalizedKey",
                table: "OnionStatuses");

            migrationBuilder.DropIndex(
                name: "IX_OnionStatuses_ParentId",
                table: "OnionStatuses");

            migrationBuilder.DropColumn(
                name: "CanonicalName",
                table: "OnionStatuses");

            migrationBuilder.DropColumn(
                name: "IsMirror",
                table: "OnionStatuses");

            migrationBuilder.DropColumn(
                name: "MirrorPriority",
                table: "OnionStatuses");

            migrationBuilder.DropColumn(
                name: "NormalizedKey",
                table: "OnionStatuses");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "OnionStatuses");
        }
    }
}
