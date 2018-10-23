using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace FxMoviesDB.Migrations
{
    public partial class AddOpinion : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_UserWatchLists",
                table: "UserWatchLists");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Users",
                table: "Users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserRatings",
                table: "UserRatings");

            migrationBuilder.DropColumn(
                name: "ImdbUserId",
                table: "UserWatchLists");

            migrationBuilder.DropColumn(
                name: "ImdbUserId",
                table: "UserRatings");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "UserWatchLists",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "ImdbUserId",
                table: "Users",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string));

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "Users",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "UserRatings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Opinion",
                table: "MovieEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserWatchLists",
                table: "UserWatchLists",
                columns: new[] { "UserId", "ImdbMovieId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_Users",
                table: "Users",
                column: "UserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserRatings",
                table: "UserRatings",
                columns: new[] { "UserId", "ImdbMovieId" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_UserWatchLists",
                table: "UserWatchLists");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Users",
                table: "Users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserRatings",
                table: "UserRatings");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "UserWatchLists");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "UserRatings");

            migrationBuilder.DropColumn(
                name: "Opinion",
                table: "MovieEvents");

            migrationBuilder.AddColumn<string>(
                name: "ImdbUserId",
                table: "UserWatchLists",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "ImdbUserId",
                table: "Users",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImdbUserId",
                table: "UserRatings",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserWatchLists",
                table: "UserWatchLists",
                columns: new[] { "ImdbUserId", "ImdbMovieId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_Users",
                table: "Users",
                column: "ImdbUserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserRatings",
                table: "UserRatings",
                columns: new[] { "ImdbUserId", "ImdbMovieId" });
        }
    }
}
