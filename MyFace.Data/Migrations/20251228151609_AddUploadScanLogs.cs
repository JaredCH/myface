using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyFace.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUploadScanLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UploadScanLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EventType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    UsernameSnapshot = table.Column<string>(type: "text", nullable: true),
                    IsAnonymous = table.Column<bool>(type: "boolean", nullable: false),
                    SessionId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IpAddressHash = table.Column<string>(type: "text", nullable: true),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    StoragePath = table.Column<string>(type: "text", nullable: true),
                    ScanEngine = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ScanStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ThreatName = table.Column<string>(type: "text", nullable: true),
                    ScannerMessage = table.Column<string>(type: "text", nullable: true),
                    Blocked = table.Column<bool>(type: "boolean", nullable: false),
                    ProcessingDurationMs = table.Column<long>(type: "bigint", nullable: false),
                    ScanDurationMs = table.Column<long>(type: "bigint", nullable: false),
                    FailureReason = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UploadScanLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UploadScanLogs_CreatedAt",
                table: "UploadScanLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UploadScanLogs_EventType_CreatedAt",
                table: "UploadScanLogs",
                columns: new[] { "EventType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UploadScanLogs_Source_CreatedAt",
                table: "UploadScanLogs",
                columns: new[] { "Source", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UploadScanLogs_UserId_CreatedAt",
                table: "UploadScanLogs",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UploadScanLogs");
        }
    }
}
