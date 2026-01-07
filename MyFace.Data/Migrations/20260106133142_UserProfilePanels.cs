using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyFace.Data.Migrations
{
    /// <inheritdoc />
    public partial class UserProfilePanels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProfilePanels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    TemplateType = table.Column<short>(type: "smallint", nullable: false),
                    PanelType = table.Column<short>(type: "smallint", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    ContentFormat = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "markdown"),
                    Position = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsVisible = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastEditedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ValidationMessage = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    RequiresModeration = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfilePanels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfilePanels_Users_LastEditedByUserId",
                        column: x => x.LastEditedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProfilePanels_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserProfileSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    TemplateType = table.Column<short>(type: "smallint", nullable: false),
                    ThemePreset = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ThemeOverridesJson = table.Column<string>(type: "text", nullable: false),
                    IsCustomHtml = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CustomHtmlPath = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CustomHtmlUploadDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CustomHtmlValidated = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CustomHtmlValidationErrors = table.Column<string>(type: "text", nullable: true),
                    CustomHtmlVersion = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LastEditedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastEditedByUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfileSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserProfileSettings_Users_LastEditedByUserId",
                        column: x => x.LastEditedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UserProfileSettings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProfilePanels_LastEditedByUserId",
                table: "ProfilePanels",
                column: "LastEditedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProfilePanels_UserId_PanelType",
                table: "ProfilePanels",
                columns: new[] { "UserId", "PanelType" });

            migrationBuilder.CreateIndex(
                name: "IX_ProfilePanels_UserId_Position",
                table: "ProfilePanels",
                columns: new[] { "UserId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_UserProfileSettings_CustomHtmlValidated",
                table: "UserProfileSettings",
                column: "CustomHtmlValidated");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfileSettings_LastEditedByUserId",
                table: "UserProfileSettings",
                column: "LastEditedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfileSettings_UserId",
                table: "UserProfileSettings",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProfilePanels");

            migrationBuilder.DropTable(
                name: "UserProfileSettings");
        }
    }
}
