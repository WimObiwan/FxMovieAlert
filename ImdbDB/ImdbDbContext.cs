using FxMovies.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace FxMovies.ImdbDB;

/// <summary>
/// The entity framework context with a Students DbSet 
/// </summary>
public class ImdbDbContext : DbContext
{
    public ImdbDbContext(DbContextOptions<ImdbDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ImdbMovie>()
            .HasKey(m => m.Id);
        modelBuilder.Entity<ImdbMovie>()
            .HasIndex(m => new { m.PrimaryTitle, m.Year });
        modelBuilder.Entity<ImdbMovie>()
            .HasIndex(m => m.ImdbId)
            .IsUnique();
        modelBuilder.Entity<ImdbMovieAlternative>()
            .HasKey(ma => ma.Id);
        modelBuilder.Entity<ImdbMovieAlternative>()
            .HasIndex(ma => new { ma.Normalized, ma.MovieId })
            .IsUnique();
        modelBuilder.Entity<ImdbMovieAlternative>()
            .HasOne(ma => ma.Movie)
            .WithMany(m => m.MovieAlternatives)
            .HasForeignKey(ma => ma.MovieId);
    }

    public DbSet<ImdbMovie> Movies { get; set; }

    public DbSet<ImdbMovieAlternative> MovieAlternatives { get; set; }
}

// /// <summary>
// /// A factory to create an instance of the StudentsContext 
// /// </summary>
// public static class ImdbDbContextFactory
// {
//     public static ImdbDbContext Create(string connectionString)
//     {
//         var optionsBuilder = new DbContextOptionsBuilder<ImdbDbContext>();
//         optionsBuilder.UseSqlite(connectionString);

//         // Ensure that the SQLite database and sechema is created!
//         var db = new ImdbDbContext(optionsBuilder.Options);
//         db.Database.EnsureCreated();

//         return db;
//     }
// }