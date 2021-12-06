using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FxMovies.MoviesDB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FxMovies.Core.Queries;

public class StatsResult
{
    public List<User> Users { get; set; }

    public class User
    {
        public string UserId { get; set; }
        public string ImdbUserId { get; set; }
        public DateTime? LastUsageTime { get; set; }
        public long Usages { get; set; }
        public int RatingCount { get; set; }
        public int WatchListItemsCount { get; set; }
    }
}

public interface IStatsQuery
{
    Task<StatsResult> Execute();
}

public class StatsQuery : IStatsQuery
{
    private readonly ILogger<ManualMatchesQuery> logger;
    private readonly MoviesDbContext moviesDbContext;

    public StatsQuery(
        ILogger<ManualMatchesQuery> logger,
        MoviesDbContext moviesDbContext)
    {
        this.logger = logger;
        this.moviesDbContext = moviesDbContext;
    }

    public async Task<StatsResult> Execute()
    {
        var statsResult = new StatsResult();
        statsResult.Users =
            await moviesDbContext.Users
                .Select(u => new StatsResult.User
                {
                    UserId = u.UserId,
                    ImdbUserId = u.ImdbUserId,
                    LastUsageTime = u.LastUsageTime,
                    Usages = u.Usages,
                    RatingCount = u.UserRatings.Count(),
                    WatchListItemsCount = u.UserWatchListItems.Count()
                })
                .ToListAsync();
        return statsResult;
    }
}