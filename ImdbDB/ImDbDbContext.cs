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
            modelBuilder.Entity<ImdbMovie>()
                .HasIndex(m => new { m.PrimaryTitle, m.Year });
            modelBuilder.Entity<MovieAlternative>()
                .HasKey(ma => new {ma.Id, ma.No});
            modelBuilder.Entity<MovieAlternative>()
                .HasIndex(ma => new { ma.AlternativeTitle });
        }

        public DbSet<ImdbMovie> Movies { get; set; }

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
