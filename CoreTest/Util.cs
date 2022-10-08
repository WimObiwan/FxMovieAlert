using FxMovies.MoviesDB;
using Microsoft.EntityFrameworkCore;

namespace FxMovies.CoreTest;

internal class Util
{
    public static  DbContextOptions<MoviesDbContext> DummyOptions { get; } = new DbContextOptionsBuilder<MoviesDbContext>().Options;
}