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

        public DbSet<MovieEvent> MovieEvents { get; set; }
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
            var context = new FxMoviesDbContext(optionsBuilder.Options);
            context.Database.EnsureCreated();

            return context;
        }
    }

    /// <summary>
    /// A simple class representing a Student
    /// </summary>
    public class MovieEvent
    {
        public MovieEvent()
        {
        }

        public int Id { get; set; }

        public string Title { get; set; }

        public int Year { get; set; }

        public DateTime StartTime { get; set; }
    }   
}
