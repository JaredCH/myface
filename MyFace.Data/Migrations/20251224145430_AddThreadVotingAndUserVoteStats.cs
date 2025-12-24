using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyFace.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddThreadVotingAndUserVoteStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Votes_SessionId_PostId",
                table: "Votes");

            migrationBuilder.DropIndex(
                name: "IX_Votes_UserId_PostId",
                table: "Votes");

            migrationBuilder.AlterColumn<int>(
                name: "PostId",
                table: "Votes",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "ThreadId",
                table: "Votes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CommentDownvotes",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CommentUpvotes",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PostDownvotes",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PostUpvotes",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Votes_SessionId_PostId",
                table: "Votes",
                columns: new[] { "SessionId", "PostId" },
                unique: true,
                filter: "\"SessionId\" IS NOT NULL AND \"PostId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Votes_SessionId_ThreadId",
                table: "Votes",
                columns: new[] { "SessionId", "ThreadId" },
                unique: true,
                filter: "\"SessionId\" IS NOT NULL AND \"ThreadId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Votes_ThreadId",
                table: "Votes",
                column: "ThreadId");

            migrationBuilder.CreateIndex(
                name: "IX_Votes_UserId_PostId",
                table: "Votes",
                columns: new[] { "UserId", "PostId" },
                unique: true,
                filter: "\"UserId\" IS NOT NULL AND \"PostId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Votes_UserId_ThreadId",
                table: "Votes",
                columns: new[] { "UserId", "ThreadId" },
                unique: true,
                filter: "\"UserId\" IS NOT NULL AND \"ThreadId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Votes_Threads_ThreadId",
                table: "Votes",
                column: "ThreadId",
                principalTable: "Threads",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Votes_Threads_ThreadId",
                table: "Votes");

            migrationBuilder.DropIndex(
                name: "IX_Votes_SessionId_PostId",
                table: "Votes");

            migrationBuilder.DropIndex(
                name: "IX_Votes_SessionId_ThreadId",
                table: "Votes");

            migrationBuilder.DropIndex(
                name: "IX_Votes_ThreadId",
                table: "Votes");

            migrationBuilder.DropIndex(
                name: "IX_Votes_UserId_PostId",
                table: "Votes");

            migrationBuilder.DropIndex(
                name: "IX_Votes_UserId_ThreadId",
                table: "Votes");

            migrationBuilder.DropColumn(
                name: "ThreadId",
                table: "Votes");

            migrationBuilder.DropColumn(
                name: "CommentDownvotes",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CommentUpvotes",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PostDownvotes",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PostUpvotes",
                table: "Users");

            migrationBuilder.AlterColumn<int>(
                name: "PostId",
                table: "Votes",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Votes_SessionId_PostId",
                table: "Votes",
                columns: new[] { "SessionId", "PostId" },
                unique: true,
                filter: "\"SessionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Votes_UserId_PostId",
                table: "Votes",
                columns: new[] { "UserId", "PostId" },
                unique: true,
                filter: "\"UserId\" IS NOT NULL");
        }
    }
}
