using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace FxMoviesDB.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Channels",
                columns: table => new
                {
                    Code = table.Column<string>(type: "TEXT", nullable: false),
                    LogoL = table.Column<string>(type: "TEXT", nullable: true),
                    LogoM = table.Column<string>(type: "TEXT", nullable: true),
                    LogoS = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Channels", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "UserRatings",
                columns: table => new
                {
                    ImdbUserId = table.Column<string>(type: "TEXT", nullable: false),
                    ImdbMovieId = table.Column<string>(type: "TEXT", nullable: false),
                    Rating = table.Column<int>(type: "INTEGER", nullable: false),
                    RatingDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRatings", x => new { x.ImdbUserId, x.ImdbMovieId });
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    ImdbUserId = table.Column<string>(type: "TEXT", nullable: false),
                    LastRefreshRatingsResult = table.Column<string>(type: "TEXT", nullable: true),
                    LastRefreshRatingsTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastRefreshSuccess = table.Column<bool>(type: "INTEGER", nullable: true),
                    LastUsageTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RefreshCount = table.Column<long>(type: "INTEGER", nullable: false),
                    RefreshRequestTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Usages = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.ImdbUserId);
                });

            migrationBuilder.CreateTable(
                name: "MovieEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Certification = table.Column<string>(type: "TEXT", nullable: true),
                    ChannelCode = table.Column<string>(type: "TEXT", nullable: true),
                    Content = table.Column<string>(type: "TEXT", nullable: true),
                    Duration = table.Column<int>(type: "INTEGER", nullable: false),
                    EndTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Genre = table.Column<string>(type: "TEXT", nullable: true),
                    ImdbId = table.Column<string>(type: "TEXT", nullable: true),
                    ImdbRating = table.Column<int>(type: "INTEGER", nullable: true),
                    ImdbVotes = table.Column<int>(type: "INTEGER", nullable: true),
                    PosterL = table.Column<string>(type: "TEXT", nullable: true),
                    PosterM = table.Column<string>(type: "TEXT", nullable: true),
                    PosterS = table.Column<string>(type: "TEXT", nullable: true),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: true),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    YeloUrl = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovieEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovieEvents_Channels_ChannelCode",
                        column: x => x.ChannelCode,
                        principalTable: "Channels",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MovieEvents_ChannelCode",
                table: "MovieEvents",
                column: "ChannelCode");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MovieEvents");

            migrationBuilder.DropTable(
                name: "UserRatings");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Channels");
        }
    }
}
