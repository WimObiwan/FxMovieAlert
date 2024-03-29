﻿// <auto-generated />
using System;
using FxMovies.MoviesDB;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FxMoviesDB.Migrations
{
    [DbContext(typeof(MoviesDbContext))]
    [Migration("20210817204703_AddManualMatch")]
    partial class AddManualMatch
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "5.0.7");

            modelBuilder.Entity("FxMovies.MoviesDB.Channel", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("Code")
                        .HasColumnType("TEXT");

                    b.Property<string>("LogoS")
                        .HasColumnType("TEXT");

                    b.Property<string>("LogoS_Local")
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("Channels");
                });

            modelBuilder.Entity("FxMovies.MoviesDB.ManualMatch", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<int?>("MovieId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("NormalizedTitle")
                        .HasColumnType("TEXT");

                    b.Property<string>("Title")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("MovieId");

                    b.ToTable("ManualMatches");
                });

            modelBuilder.Entity("FxMovies.MoviesDB.Movie", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("Certification")
                        .HasColumnType("TEXT");

                    b.Property<string>("ImdbId")
                        .HasColumnType("TEXT");

                    b.Property<bool>("ImdbIgnore")
                        .HasColumnType("INTEGER");

                    b.Property<int?>("ImdbRating")
                        .HasColumnType("INTEGER");

                    b.Property<int?>("ImdbVotes")
                        .HasColumnType("INTEGER");

                    b.Property<string>("OriginalTitle")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("ImdbId")
                        .IsUnique();

                    b.ToTable("Movies");
                });

            modelBuilder.Entity("FxMovies.MoviesDB.MovieEvent", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<DateTime?>("AddedTime")
                        .HasColumnType("TEXT");

                    b.Property<int?>("ChannelId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Content")
                        .HasColumnType("TEXT");

                    b.Property<int?>("Duration")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime?>("EndTime")
                        .HasColumnType("TEXT");

                    b.Property<string>("ExternalId")
                        .HasColumnType("TEXT");

                    b.Property<string>("Genre")
                        .HasColumnType("TEXT");

                    b.Property<int?>("MovieId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Opinion")
                        .HasColumnType("TEXT");

                    b.Property<string>("PosterM")
                        .HasColumnType("TEXT");

                    b.Property<string>("PosterM_Local")
                        .HasColumnType("TEXT");

                    b.Property<string>("PosterS")
                        .HasColumnType("TEXT");

                    b.Property<string>("PosterS_Local")
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("StartTime")
                        .HasColumnType("TEXT");

                    b.Property<string>("Title")
                        .HasColumnType("TEXT");

                    b.Property<int?>("Type")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("Vod")
                        .HasColumnType("INTEGER");

                    b.Property<string>("VodLink")
                        .HasColumnType("TEXT");

                    b.Property<int?>("Year")
                        .HasColumnType("INTEGER");

                    b.Property<string>("YeloUrl")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("ChannelId");

                    b.HasIndex("MovieId");

                    b.ToTable("MovieEvents");
                });

            modelBuilder.Entity("FxMovies.MoviesDB.User", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("ImdbUserId")
                        .HasColumnType("TEXT");

                    b.Property<string>("LastRefreshRatingsResult")
                        .HasColumnType("TEXT");

                    b.Property<DateTime?>("LastRefreshRatingsTime")
                        .HasColumnType("TEXT");

                    b.Property<bool?>("LastRefreshSuccess")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime?>("LastUsageTime")
                        .HasColumnType("TEXT");

                    b.Property<long>("RefreshCount")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime?>("RefreshRequestTime")
                        .HasColumnType("TEXT");

                    b.Property<long>("Usages")
                        .HasColumnType("INTEGER");

                    b.Property<string>("UserId")
                        .HasColumnType("TEXT");

                    b.Property<string>("WatchListLastRefreshResult")
                        .HasColumnType("TEXT");

                    b.Property<bool?>("WatchListLastRefreshSuccess")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime?>("WatchListLastRefreshTime")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("ImdbUserId")
                        .IsUnique();

                    b.ToTable("Users");
                });

            modelBuilder.Entity("FxMovies.MoviesDB.UserRating", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<int?>("MovieId")
                        .HasColumnType("INTEGER");

                    b.Property<int>("Rating")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("RatingDate")
                        .HasColumnType("TEXT");

                    b.Property<int?>("UserId")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("MovieId");

                    b.HasIndex("UserId", "MovieId");

                    b.ToTable("UserRatings");
                });

            modelBuilder.Entity("FxMovies.MoviesDB.UserWatchListItem", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("AddedDate")
                        .HasColumnType("TEXT");

                    b.Property<int?>("MovieId")
                        .HasColumnType("INTEGER");

                    b.Property<int?>("UserId")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("MovieId");

                    b.HasIndex("UserId", "MovieId");

                    b.ToTable("UserWatchLists");
                });

            modelBuilder.Entity("FxMovies.MoviesDB.ManualMatch", b =>
                {
                    b.HasOne("FxMovies.MoviesDB.Movie", "Movie")
                        .WithMany("ManualMatches")
                        .HasForeignKey("MovieId");

                    b.Navigation("Movie");
                });

            modelBuilder.Entity("FxMovies.MoviesDB.MovieEvent", b =>
                {
                    b.HasOne("FxMovies.MoviesDB.Channel", "Channel")
                        .WithMany()
                        .HasForeignKey("ChannelId");

                    b.HasOne("FxMovies.MoviesDB.Movie", "Movie")
                        .WithMany("MovieEvents")
                        .HasForeignKey("MovieId");

                    b.Navigation("Channel");

                    b.Navigation("Movie");
                });

            modelBuilder.Entity("FxMovies.MoviesDB.UserRating", b =>
                {
                    b.HasOne("FxMovies.MoviesDB.Movie", "Movie")
                        .WithMany("UserRatings")
                        .HasForeignKey("MovieId");

                    b.HasOne("FxMovies.MoviesDB.User", "User")
                        .WithMany("UserRatings")
                        .HasForeignKey("UserId");

                    b.Navigation("Movie");

                    b.Navigation("User");
                });

            modelBuilder.Entity("FxMovies.MoviesDB.UserWatchListItem", b =>
                {
                    b.HasOne("FxMovies.MoviesDB.Movie", "Movie")
                        .WithMany("UserWatchListItems")
                        .HasForeignKey("MovieId");

                    b.HasOne("FxMovies.MoviesDB.User", "User")
                        .WithMany("UserWatchListItems")
                        .HasForeignKey("UserId");

                    b.Navigation("Movie");

                    b.Navigation("User");
                });

            modelBuilder.Entity("FxMovies.MoviesDB.Movie", b =>
                {
                    b.Navigation("ManualMatches");

                    b.Navigation("MovieEvents");

                    b.Navigation("UserRatings");

                    b.Navigation("UserWatchListItems");
                });

            modelBuilder.Entity("FxMovies.MoviesDB.User", b =>
                {
                    b.Navigation("UserRatings");

                    b.Navigation("UserWatchListItems");
                });
#pragma warning restore 612, 618
        }
    }
}
