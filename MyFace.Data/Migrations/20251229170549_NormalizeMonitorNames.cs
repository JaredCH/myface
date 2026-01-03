using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyFace.Data.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeMonitorNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE ""OnionStatuses""
                SET ""Name"" = regexp_replace(""Name"", E'\\s*\\(mirror[^)]*\\)$', '', 'gi')
                WHERE ""Name"" ~* E'\\(mirror';
            ");

            migrationBuilder.Sql(@"
                UPDATE ""OnionStatuses""
                SET ""OnionUrl"" = 'http://' || ""OnionUrl""
                WHERE ""OnionUrl"" IS NOT NULL
                  AND ""OnionUrl"" NOT ILIKE 'http://%'
                  AND ""OnionUrl"" NOT ILIKE 'https://%';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
