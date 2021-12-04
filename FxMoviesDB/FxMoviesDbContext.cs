using FxMovies.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace FxMovies.FxMoviesDB;

/// <summary>
/// The entity framework context with a Students DbSet 
/// </summary>
public class FxMoviesDbContext : DbContext
{
    public FxMoviesDbContext(DbContextOptions<FxMoviesDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Movie>()
            .HasKey(m => m.Id);
        modelBuilder.Entity<Movie>()
            .HasIndex(m => m.ImdbId);
        modelBuilder.Entity<Movie>()
            .HasMany(m => m.MovieEvents)
            .WithOne(me => me.Movie);
        modelBuilder.Entity<Movie>()
            .HasMany(m => m.UserRatings)
            .WithOne(ur => ur.Movie)
            .HasForeignKey(ur => ur.MovieId);
        modelBuilder.Entity<Movie>()
            .HasMany(m => m.UserWatchListItems)
            .WithOne(uw => uw.Movie)
            .HasForeignKey(uw => uw.MovieId);
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
        modelBuilder.Entity<User>()
            .HasMany(u => u.UserWatchListItems)
            .WithOne(uw => uw.User)
            .HasForeignKey(uw => uw.UserId);
        modelBuilder.Entity<UserWatchListItem>()
            .HasKey(uw => uw.Id);
        modelBuilder.Entity<UserWatchListItem>()
            .HasIndex(uw => new { uw.UserId, uw.MovieId });
        modelBuilder.Entity<MovieEvent>()
            .HasKey(me => me.Id);
        modelBuilder.Entity<ManualMatch>()
            .HasKey(m => m.Id);
        modelBuilder.Entity<Movie>()
            .HasMany(m => m.ManualMatches)
            .WithOne(mm => mm.Movie)
            .HasForeignKey(mm => mm.MovieId);
    }

    public DbSet<Channel> Channels { get; set; }
    public DbSet<Movie> Movies { get; set; }
    public DbSet<MovieEvent> MovieEvents { get; set; }
    public DbSet<UserRating> UserRatings { get; set; }
    public DbSet<UserWatchListItem> UserWatchLists { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<ManualMatch> ManualMatches { get; set; }
}

// /// <summary>
// /// A factory to create an instance of the StudentsContext 
// /// </summary>
// public static class FxMoviesDbContextFactory
// {
//     public static FxMoviesDbContext Create(string connectionString)
//     {
//         var optionsBuilder = new DbContextOptionsBuilder<FxMoviesDbContext>();
//         optionsBuilder.UseSqlite(connectionString);

//         // Ensure that the SQLite database and sechema is created!
//         var db = new FxMoviesDbContext(optionsBuilder.Options);
//         db.Database.EnsureCreated();

//         return db;
//     }
// }