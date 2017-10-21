using System;
using Microsoft.EntityFrameworkCore;

namespace FxMovies.ImdbDB
{
    /// <summary>
    /// The entity framework context with a Students DbSet 
    /// </summary>
    public class ImdbDbContext : DbContext
    {
        public ImdbDbContext(DbContextOptions<ImdbDbContext> options)
            : base(options)
        { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Movie>()
                .HasIndex(m => new { m.PrimaryTitle, m.Year });
            modelBuilder.Entity<MovieAlternative>()
                .HasIndex(m => new { m.AlternativeTitle });
        }

        public DbSet<Movie> Movies { get; set; }

        public DbSet<MovieAlternative> MovieAlternatives { get; set; }
    }

    /// <summary>
    /// A factory to create an instance of the StudentsContext 
    /// </summary>
    public static class ImdbDbContextFactory
    {
        public static ImdbDbContext Create(string connectionString)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ImdbDbContext>();
            optionsBuilder.UseSqlite(connectionString);

            // Ensure that the SQLite database and sechema is created!
            var db = new ImdbDbContext(optionsBuilder.Options);
            db.Database.EnsureCreated();

            return db;
        }
    }
}
