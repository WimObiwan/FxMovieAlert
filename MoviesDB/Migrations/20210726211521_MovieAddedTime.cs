using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FxMoviesDB.Migrations
{
    public partial class MovieAddedTime : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AddedTime",
                table: "MovieEvents",
                type: "TEXT",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AddedTime",
                table: "MovieEvents");
        }
    }
}
