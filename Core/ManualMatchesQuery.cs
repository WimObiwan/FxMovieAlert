using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FxMovies.FxMoviesDB;
using FxMovies.ImdbDB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FxMovies.Core
{
    public interface IManualMatchesQuery
    {
        Task<ManualMatch> Run(string movieTitle);
    }

    public class ManualMatchesQuery : IManualMatchesQuery
    {
        private readonly ILogger<ImdbMatchingQuery> logger;
        private readonly FxMoviesDbContext fxMoviesDbContext;

        public ManualMatchesQuery(
            ILogger<ImdbMatchingQuery> logger,
            FxMoviesDbContext fxMoviesDbContext)
        {
            this.logger = logger;
            this.fxMoviesDbContext = fxMoviesDbContext;
        }

        public async Task<ManualMatch> Run(string movieTitle)
        {
            string movieTitleNormalized = ImdbDB.Util.NormalizeTitle(movieTitle);
            var manualMatch = await fxMoviesDbContext.ManualMatches
                .Include(mm => mm.Movie)
                .FirstOrDefaultAsync(mm => mm.NormalizedTitle == movieTitleNormalized);
            return manualMatch;
        }
    }
}