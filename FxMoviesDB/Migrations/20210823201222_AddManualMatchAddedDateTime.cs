using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FxMoviesDB.Migrations
{
    public partial class AddManualMatchAddedDateTime : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AddedDateTime",
                table: "ManualMatches",
                type: "TEXT",
                nullable: false,
                defaultValue: DateTime.UtcNow);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AddedDateTime",
                table: "ManualMatches");
        }
    }
}
