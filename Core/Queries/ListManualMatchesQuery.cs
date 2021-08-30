using System.Collections.Generic;
using System.Threading.Tasks;
using FxMovies.FxMoviesDB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FxMovies.Core.Queries
{
    public interface IListManualMatchesQuery
    {
        Task<List<ManualMatch>> Execute();
    }

    public class ListManualMatchesQuery : IListManualMatchesQuery
    {
        private readonly ILogger<ListManualMatchesQuery> logger;
        private readonly FxMoviesDbContext fxMoviesDbContext;

        public ListManualMatchesQuery(
            ILogger<ListManualMatchesQuery> logger,
            FxMoviesDbContext fxMoviesDbContext)
        {
            this.logger = logger;
            this.fxMoviesDbContext = fxMoviesDbContext;
        }

        public async Task<List<ManualMatch>> Execute()
        {
            return await fxMoviesDbContext.ManualMatches
                .Include(mm => mm.Movie)
                .ToListAsync();
        }
    }
}