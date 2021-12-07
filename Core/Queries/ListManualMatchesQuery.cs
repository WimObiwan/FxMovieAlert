using System.Collections.Generic;
using System.Threading.Tasks;
using FxMovies.Core.Entities;
using FxMovies.MoviesDB;
using Microsoft.EntityFrameworkCore;

namespace FxMovies.Core.Queries;

public interface IListManualMatchesQuery
{
    Task<List<ManualMatch>> Execute();
}

public class ListManualMatchesQuery : IListManualMatchesQuery
{
    private readonly MoviesDbContext _moviesDbContext;

    public ListManualMatchesQuery(
        MoviesDbContext moviesDbContext)
    {
        _moviesDbContext = moviesDbContext;
    }

    public async Task<List<ManualMatch>> Execute()
    {
        return await _moviesDbContext.ManualMatches
            .Include(mm => mm.Movie)
            .ToListAsync();
    }
}