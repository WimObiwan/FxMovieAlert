using System;
using Microsoft.EntityFrameworkCore;

namespace FxMovies.FxMoviesDB
{
    /// <summary>
    /// The entity framework context with a Students DbSet 
    /// </summary>
    public class FxMoviesDbContext : DbContext
    {
        public FxMoviesDbContext(DbContextOptions<FxMoviesDbContext> options)
            : base(options)
        { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Movie>()
                .HasKey(m => m.Id);
            modelBuilder.Entity<Movie>()
                .HasIndex(m => m.ImdbId)
                .IsUnique();
            modelBuilder.Entity<Movie>()
                .HasMany(m => m.MovieEvents)
                .WithOne(me => me.Movie);
            modelBuilder.Entity<Movie>()
                .HasMany(m => m.UserRatings)
                .WithOne(ur => ur.Movie)
                .HasForeignKey(ur => ur.MovieId);
            modelBuilder.Entity<User>()
                .HasKey(u => u.Id);
            modelBuilder.Entity<User>()
                .HasIndex(u => u.ImdbUserId)
                .IsUnique();
            modelBuilder.Entity<User>()
                .HasMany(u => u.UserRatings)
                .WithOne(ur => ur.User)
                .HasForeignKey(ur => ur.UserId);
            modelBuilder.Entity<UserRating>()
                .HasKey(ur => ur.Id);
            modelBuilder.Entity<UserRating>()
                .HasIndex(ur => new { ur.UserId, ur.MovieId });
            modelBuilder.Entity<UserWatchListItem>()
                .HasKey(u => new { u.UserId, u.ImdbMovieId });
        }

        public DbSet<Channel> Channels { get; set; }
        public DbSet<Movie> Movies { get; set; }
        public DbSet<MovieEvent> MovieEvents { get; set; }
        public DbSet<UserRating> UserRatings { get; set; }
        public DbSet<UserWatchListItem> UserWatchLists { get; set; }
        public DbSet<User> Users { get; set; }
    }

    /// <summary>
    /// A factory to create an instance of the StudentsContext 
    /// </summary>
    public static class FxMoviesDbContextFactory
    {
        public static FxMoviesDbContext Create(string connectionString)
        {
            var optionsBuilder = new DbContextOptionsBuilder<FxMoviesDbContext>();
            optionsBuilder.UseSqlite(connectionString);

            // Ensure that the SQLite database and sechema is created!
            var db = new FxMoviesDbContext(optionsBuilder.Options);
            db.Database.EnsureCreated();

            return db;
        }
    }
}
