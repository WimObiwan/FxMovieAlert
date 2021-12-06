using System.Threading.Tasks;
using FxMovies.Core.Entities;
using FxMovies.Core.Utilities;
using FxMovies.MoviesDB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FxMovies.Core.Queries;

public interface IManualMatchesQuery
{
    Task<ManualMatch> Execute(string movieTitle);
}

public class ManualMatchesQuery : IManualMatchesQuery
{
    private readonly ILogger<ManualMatchesQuery> logger;
    private readonly MoviesDbContext moviesDbContext;

    public ManualMatchesQuery(
        ILogger<ManualMatchesQuery> logger,
        MoviesDbContext moviesDbContext)
    {
        this.logger = logger;
        this.moviesDbContext = moviesDbContext;
    }

    public async Task<ManualMatch> Execute(string movieTitle)
    {
        var movieTitleNormalized = TitleNormalizer.NormalizeTitle(movieTitle);
        var manualMatch = await moviesDbContext.ManualMatches
            .Include(mm => mm.Movie)
            .FirstOrDefaultAsync(mm => mm.NormalizedTitle == movieTitleNormalized);
        return manualMatch;
    }
}