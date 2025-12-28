using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FxMoviesDB.Migrations
{
    /// <inheritdoc />
    public partial class FixLegacyWatchlistDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Convert legacy DateTime.MinValue (0001-01-01) to null
            migrationBuilder.Sql(
                "UPDATE UserWatchLists SET AddedDate = NULL WHERE AddedDate = '0001-01-01 00:00:00'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
