using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FxMoviesDB.Migrations
{
    public partial class AddMovieEventIgnore : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Ignore",
                table: "MovieEvents",
                type: "INTEGER",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Ignore",
                table: "MovieEvents");
        }
    }
}
