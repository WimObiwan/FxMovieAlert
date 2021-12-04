using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FxMovies.FxMoviesDB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FxMovies.Core.Queries;

public class StatsResult
{
    public class User
    {
        public string UserId { get; set; }
        public string ImdbUserId { get; set; }
        public DateTime? LastUsageTime { get; set; }
        public long Usages { get; set; }
        public int RatingCount { get; set; }
        public int WatchListItemsCount { get; set; }
    }

    public List<User> Users { get; set; }
}

public interface IStatsQuery
{
    Task<StatsResult> Execute();
}

public class StatsQuery : IStatsQuery
{
    private readonly ILogger<ManualMatchesQuery> logger;
    private readonly FxMoviesDbContext fxMoviesDbContext;

    public StatsQuery(
        ILogger<ManualMatchesQuery> logger,
        FxMoviesDbContext fxMoviesDbContext)
    {
        this.logger = logger;
        this.fxMoviesDbContext = fxMoviesDbContext;
    }

    public async Task<StatsResult> Execute()
    {
        var statsResult = new StatsResult();
        statsResult.Users =
            await fxMoviesDbContext.Users
                .Select(u => new StatsResult.User()
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