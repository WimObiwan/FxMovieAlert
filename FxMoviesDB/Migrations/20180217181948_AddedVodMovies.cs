using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace FxMoviesDB.Migrations
{
    public partial class AddedVodMovies : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // migrationBuilder.AlterColumn<int>(
            //     name: "Year",
            //     table: "MovieEvents",
            //     type: "INTEGER",
            //     nullable: true,
            //     oldClrType: typeof(int));

            migrationBuilder.CreateTable(
                name: "VodMovies",
                columns: table => new
                {
                    Provider = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderCategory = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderId = table.Column<int>(type: "INTEGER", nullable: false),
                    Certification = table.Column<string>(type: "TEXT", nullable: true),
                    Image = table.Column<string>(type: "TEXT", nullable: true),
                    Image_Local = table.Column<string>(type: "TEXT", nullable: true),
                    ImdbId = table.Column<string>(type: "TEXT", nullable: true),
                    ImdbRating = table.Column<int>(type: "INTEGER", nullable: true),
                    ImdbVotes = table.Column<int>(type: "INTEGER", nullable: true),
                    Price = table.Column<decimal>(type: "TEXT", nullable: false),
                    PrividerId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProviderMask = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: true),
                    ValidFrom = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ValidUntil = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VodMovies", x => new { x.Provider, x.ProviderCategory, x.ProviderId });
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VodMovies");

            // migrationBuilder.AlterColumn<int>(
            //     name: "Year",
            //     table: "MovieEvents",
            //     nullable: false,
            //     oldClrType: typeof(int),
            //     oldType: "INTEGER",
            //     oldNullable: true);
        }
    }
}
