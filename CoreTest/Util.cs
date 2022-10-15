using FxMovies.ImdbDB;
using FxMovies.MoviesDB;
using Microsoft.EntityFrameworkCore;

namespace FxMovies.CoreTest;

internal class Util
{
    public static DbContextOptions<MoviesDbContext> DummyMoviesDbOptions { get; } =
        new DbContextOptionsBuilder<MoviesDbContext>().Options;

    public static DbContextOptions<ImdbDbContext> DummyImdbDbOptions { get; } =
        new DbContextOptionsBuilder<ImdbDbContext>().Options;
}