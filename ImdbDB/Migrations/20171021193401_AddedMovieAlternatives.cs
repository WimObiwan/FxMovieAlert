using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace ImdbDB.Migrations
{
    public partial class AddedMovieAlternatives : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MovieAlternatives",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    AlternativeTitle = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovieAlternatives", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MovieAlternatives_AlternativeTitle",
                table: "MovieAlternatives",
                column: "AlternativeTitle");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MovieAlternatives");
        }
    }
}
