using System;
using Microsoft.EntityFrameworkCore.Migrations;

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
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    LogoS = table.Column<string>(type: "TEXT", nullable: true),
                    LogoS_Local = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Channels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Movies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ImdbId = table.Column<string>(type: "TEXT", nullable: true),
                    ImdbRating = table.Column<int>(type: "INTEGER", nullable: true),
                    ImdbVotes = table.Column<int>(type: "INTEGER", nullable: true),
                    Certification = table.Column<string>(type: "TEXT", nullable: true),
                    OriginalTitle = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Movies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: true),
                    ImdbUserId = table.Column<string>(type: "TEXT", nullable: true),
                    RefreshRequestTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastRefreshRatingsTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastRefreshSuccess = table.Column<bool>(type: "INTEGER", nullable: true),
                    LastRefreshRatingsResult = table.Column<string>(type: "TEXT", nullable: true),
                    RefreshCount = table.Column<long>(type: "INTEGER", nullable: false),
                    LastUsageTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Usages = table.Column<long>(type: "INTEGER", nullable: false),
                    WatchListLastRefreshTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    WatchListLastRefreshSuccess = table.Column<bool>(type: "INTEGER", nullable: true),
                    WatchListLastRefreshResult = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MovieEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ExternalId = table.Column<string>(type: "TEXT", nullable: true),
                    Type = table.Column<int>(type: "INTEGER", nullable: true),
                    Title = table.Column<string>(type: "TEXT", nullable: true),
                    Year = table.Column<int>(type: "INTEGER", nullable: true),
                    Vod = table.Column<bool>(type: "INTEGER", nullable: false),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ChannelId = table.Column<int>(type: "INTEGER", nullable: true),
                    PosterS = table.Column<string>(type: "TEXT", nullable: true),
                    PosterM = table.Column<string>(type: "TEXT", nullable: true),
                    Duration = table.Column<int>(type: "INTEGER", nullable: true),
                    Genre = table.Column<string>(type: "TEXT", nullable: true),
                    Content = table.Column<string>(type: "TEXT", nullable: true),
                    Opinion = table.Column<string>(type: "TEXT", nullable: true),
                    MovieId = table.Column<int>(type: "INTEGER", nullable: true),
                    YeloUrl = table.Column<string>(type: "TEXT", nullable: true),
                    PosterS_Local = table.Column<string>(type: "TEXT", nullable: true),
                    PosterM_Local = table.Column<string>(type: "TEXT", nullable: true),
                    VodLink = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovieEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovieEvents_Channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "Channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovieEvents_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserRatings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RatingDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Rating = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true),
                    MovieId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRatings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserRatings_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserRatings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserWatchLists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AddedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true),
                    MovieId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserWatchLists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserWatchLists_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserWatchLists_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MovieEvents_ChannelId",
                table: "MovieEvents",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_MovieEvents_MovieId",
                table: "MovieEvents",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_Movies_ImdbId",
                table: "Movies",
                column: "ImdbId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRatings_MovieId",
                table: "UserRatings",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRatings_UserId_MovieId",
                table: "UserRatings",
                columns: new[] { "UserId", "MovieId" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_ImdbUserId",
                table: "Users",
                column: "ImdbUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserWatchLists_MovieId",
                table: "UserWatchLists",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_UserWatchLists_UserId_MovieId",
                table: "UserWatchLists",
                columns: new[] { "UserId", "MovieId" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MovieEvents");

            migrationBuilder.DropTable(
                name: "UserRatings");

            migrationBuilder.DropTable(
                name: "UserWatchLists");

            migrationBuilder.DropTable(
                name: "Channels");

            migrationBuilder.DropTable(
                name: "Movies");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
