using Microsoft.EntityFrameworkCore.Migrations;

namespace FxMoviesDB.Migrations
{
    public partial class AddImdbIgnore : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ImdbIgnore",
                table: "Movies",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImdbIgnore",
                table: "Movies");
        }
    }
}
