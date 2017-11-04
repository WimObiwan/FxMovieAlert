using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace FxMoviesDB.Migrations
{
    public partial class AddedUserWatchList : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WatchListLastRefreshResult",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WatchListLastRefreshSuccess",
                table: "Users",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WatchListLastRefreshTime",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserWatchLists",
                columns: table => new
                {
                    ImdbUserId = table.Column<string>(type: "TEXT", nullable: false),
                    ImdbMovieId = table.Column<string>(type: "TEXT", nullable: false),
                    AddedDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserWatchLists", x => new { x.ImdbUserId, x.ImdbMovieId });
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserWatchLists");

            migrationBuilder.DropColumn(
                name: "WatchListLastRefreshResult",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "WatchListLastRefreshSuccess",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "WatchListLastRefreshTime",
                table: "Users");
        }
    }
}
