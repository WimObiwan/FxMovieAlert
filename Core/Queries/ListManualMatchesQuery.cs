using System.Collections.Generic;
using System.Threading.Tasks;
using FxMovies.Core.Entities;
using FxMovies.MoviesDB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FxMovies.Core.Queries;

public interface IListManualMatchesQuery
{
    Task<List<ManualMatch>> Execute();
}

public class ListManualMatchesQuery : IListManualMatchesQuery
{
    private readonly ILogger<ListManualMatchesQuery> _logger;
    private readonly MoviesDbContext _moviesDbContext;

    public ListManualMatchesQuery(
        ILogger<ListManualMatchesQuery> logger,
        MoviesDbContext moviesDbContext)
    {
        _logger = logger;
        _moviesDbContext = moviesDbContext;
    }

    public async Task<List<ManualMatch>> Execute()
    {
        return await _moviesDbContext.ManualMatches
            .Include(mm => mm.Movie)
            .ToListAsync();
    }
}