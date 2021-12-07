using System.Threading.Tasks;
using FxMovies.Core.Entities;
using FxMovies.Core.Utilities;
using FxMovies.MoviesDB;
using Microsoft.EntityFrameworkCore;

namespace FxMovies.Core.Queries;

public interface IManualMatchesQuery
{
    Task<ManualMatch> Execute(string movieTitle);
}

public class ManualMatchesQuery : IManualMatchesQuery
{
    private readonly MoviesDbContext _moviesDbContext;

    public ManualMatchesQuery(
        MoviesDbContext moviesDbContext)
    {
        _moviesDbContext = moviesDbContext;
    }

    public async Task<ManualMatch> Execute(string movieTitle)
    {
        var movieTitleNormalized = TitleNormalizer.NormalizeTitle(movieTitle);
        var manualMatch = await _moviesDbContext.ManualMatches
            .Include(mm => mm.Movie)
            .FirstOrDefaultAsync(mm => mm.NormalizedTitle == movieTitleNormalized);
        return manualMatch;
    }
}