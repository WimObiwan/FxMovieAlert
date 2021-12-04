using System.Threading.Tasks;
using FxMovies.Core.Entities;
using FxMovies.Core.Utilities;
using FxMovies.FxMoviesDB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FxMovies.Core.Queries;

public interface IManualMatchesQuery
{
    Task<ManualMatch> Execute(string movieTitle);
}

public class ManualMatchesQuery : IManualMatchesQuery
{
    private readonly FxMoviesDbContext fxMoviesDbContext;
    private readonly ILogger<ManualMatchesQuery> logger;

    public ManualMatchesQuery(
        ILogger<ManualMatchesQuery> logger,
        FxMoviesDbContext fxMoviesDbContext)
    {
        this.logger = logger;
        this.fxMoviesDbContext = fxMoviesDbContext;
    }

    public async Task<ManualMatch> Execute(string movieTitle)
    {
        var movieTitleNormalized = TitleNormalizer.NormalizeTitle(movieTitle);
        var manualMatch = await fxMoviesDbContext.ManualMatches
            .Include(mm => mm.Movie)
            .FirstOrDefaultAsync(mm => mm.NormalizedTitle == movieTitleNormalized);
        return manualMatch;
    }
}