using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyFace.Data.Migrations
{
    /// <inheritdoc />
    public partial class ContentModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WordListEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WordPattern = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    MatchType = table.Column<int>(type: "integer", nullable: false),
                    ActionType = table.Column<int>(type: "integer", nullable: false),
                    MuteDurationHours = table.Column<int>(type: "integer", nullable: true),
                    ReplacementText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CaseSensitive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    AppliesTo = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WordListEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WordListEntries_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserInfractions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    ContentId = table.Column<int>(type: "integer", nullable: true),
                    ContentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MatchedPattern = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    WordListEntryId = table.Column<int>(type: "integer", nullable: true),
                    ActionTaken = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    MuteExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SessionFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    TorFingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    OriginalContent = table.Column<string>(type: "text", nullable: true),
                    ContentModified = table.Column<bool>(type: "boolean", nullable: false),
                    IsEscalation = table.Column<bool>(type: "boolean", nullable: false),
                    AdminNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserInfractions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserInfractions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserInfractions_WordListEntries_WordListEntryId",
                        column: x => x.WordListEntryId,
                        principalTable: "WordListEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserInfractions_MuteExpiresAt",
                table: "UserInfractions",
                column: "MuteExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserInfractions_OccurredAt",
                table: "UserInfractions",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserInfractions_SessionFingerprint",
                table: "UserInfractions",
                column: "SessionFingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_UserInfractions_TorFingerprint",
                table: "UserInfractions",
                column: "TorFingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_UserInfractions_UserId",
                table: "UserInfractions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserInfractions_UserId_MuteExpiresAt",
                table: "UserInfractions",
                columns: new[] { "UserId", "MuteExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserInfractions_WordListEntryId",
                table: "UserInfractions",
                column: "WordListEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_WordListEntries_CreatedAt",
                table: "WordListEntries",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WordListEntries_CreatedByUserId",
                table: "WordListEntries",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WordListEntries_Enabled",
                table: "WordListEntries",
                column: "Enabled");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserInfractions");

            migrationBuilder.DropTable(
                name: "WordListEntries");
        }
    }
}
