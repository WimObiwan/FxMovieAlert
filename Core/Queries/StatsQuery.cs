using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FxMovies.MoviesDB;
using Microsoft.EntityFrameworkCore;

namespace FxMovies.Core.Queries;

public class StatsResult
{
    public List<User> Users { get; init; }

    public class User
    {
        public string UserId { get; init; }
        public string ImdbUserId { get; init; }
        public DateTime? LastUsageTime { get; init; }
        public long Usages { get; init; }
        public int RatingCount { get; init; }
        public int WatchListItemsCount { get; init; }
    }
}

public interface IStatsQuery
{
    Task<StatsResult> Execute();
}

public class StatsQuery : IStatsQuery
{
    private readonly MoviesDbContext _moviesDbContext;

    public StatsQuery(
        MoviesDbContext moviesDbContext)
    {
        _moviesDbContext = moviesDbContext;
    }

    public async Task<StatsResult> Execute()
    {
        return new StatsResult
        {
            Users =
                await _moviesDbContext.Users
                    .Select(u => new StatsResult.User
                    {
                        UserId = u.UserId,
                        ImdbUserId = u.ImdbUserId,
                        LastUsageTime = u.LastUsageTime,
                        Usages = u.Usages,
                        RatingCount = u.UserRatings.Count,
                        WatchListItemsCount = u.UserWatchListItems.Count
                    })
                    .ToListAsync()
        };
    }
}