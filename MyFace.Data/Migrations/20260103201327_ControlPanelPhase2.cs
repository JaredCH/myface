using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyFace.Data.Migrations
{
    /// <inheritdoc />
    public partial class ControlPanelPhase2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS "PrivateMessages" (
                    "Id" SERIAL PRIMARY KEY,
                    "SenderId" integer NULL,
                    "RecipientId" integer NULL,
                    "SenderUsernameSnapshot" varchar(50) NOT NULL,
                    "RecipientUsernameSnapshot" varchar(50) NOT NULL,
                    "Subject" varchar(200) NOT NULL,
                    "Body" text NOT NULL,
                    "IsDraft" boolean NOT NULL DEFAULT false,
                    "CreatedAt" timestamptz NOT NULL DEFAULT NOW(),
                    "SentAt" timestamptz NULL,
                    "ReadAt" timestamptz NULL,
                    "SenderDeleted" boolean NOT NULL DEFAULT false,
                    "RecipientDeleted" boolean NOT NULL DEFAULT false
                );
                CREATE INDEX IF NOT EXISTS "IX_PrivateMessages_RecipientId_CreatedAt" ON "PrivateMessages" ("RecipientId", "CreatedAt");
                CREATE INDEX IF NOT EXISTS "IX_PrivateMessages_SenderId_CreatedAt" ON "PrivateMessages" ("SenderId", "CreatedAt");
                """
            );

            migrationBuilder.Sql(
                """
                ALTER TABLE "PrivateMessages"
                ADD COLUMN IF NOT EXISTS "RecipientDeleted" boolean NOT NULL DEFAULT FALSE;
                """
            );

            migrationBuilder.Sql(
                """
                ALTER TABLE "PrivateMessages"
                ADD COLUMN IF NOT EXISTS "SenderDeleted" boolean NOT NULL DEFAULT FALSE;
                """
            );

            migrationBuilder.Sql(
                """
                ALTER TABLE "PageVisits"
                ADD COLUMN IF NOT EXISTS "Referrer" character varying(512);
                """
            );

            migrationBuilder.CreateTable(
                name: "ControlPanelAuditEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ActorUserId = table.Column<int>(type: "integer", nullable: true),
                    ActorUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ActorRole = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Action = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Target = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Details = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControlPanelAuditEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ControlSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    UpdatedByUsername = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControlSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ControlPanelAuditEntries_Action",
                table: "ControlPanelAuditEntries",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_ControlPanelAuditEntries_CreatedAt",
                table: "ControlPanelAuditEntries",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ControlSettings_Key",
                table: "ControlSettings",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ControlPanelAuditEntries");

            migrationBuilder.DropTable(
                name: "ControlSettings");

            migrationBuilder.Sql(
                """
                ALTER TABLE "PrivateMessages"
                DROP COLUMN IF EXISTS "RecipientDeleted";
                """
            );

            migrationBuilder.Sql(
                """
                ALTER TABLE "PrivateMessages"
                DROP COLUMN IF EXISTS "SenderDeleted";
                """
            );

            migrationBuilder.Sql(
                """
                ALTER TABLE "PageVisits"
                DROP COLUMN IF EXISTS "Referrer";
                """
            );
        }
    }
}
