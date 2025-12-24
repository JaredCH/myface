using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyFace.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVisitTrackingAndUsernameChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MustChangeUsername",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "UsernameChangedByAdminId",
                table: "Users",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PageVisits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VisitedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IpHash = table.Column<string>(type: "text", nullable: true),
                    UserAgent = table.Column<string>(type: "text", nullable: true),
                    Path = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageVisits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UsernameChangeLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    OldUsername = table.Column<string>(type: "text", nullable: false),
                    NewUsername = table.Column<string>(type: "text", nullable: false),
                    ChangedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserNotified = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsernameChangeLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsernameChangeLogs_Users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UsernameChangeLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UsernameChangeLogs_ChangedByUserId",
                table: "UsernameChangeLogs",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UsernameChangeLogs_UserId",
                table: "UsernameChangeLogs",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PageVisits");

            migrationBuilder.DropTable(
                name: "UsernameChangeLogs");

            migrationBuilder.DropColumn(
                name: "MustChangeUsername",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "UsernameChangedByAdminId",
                table: "Users");
        }
    }
}
