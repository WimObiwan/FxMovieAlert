using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FxMovies.FxMoviesDB;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FxMovies.Core
{
    public interface IUsersRepository
    {
        IAsyncEnumerable<string> GetAllImdbUserIds();
        IAsyncEnumerable<User> GetAllImdbUsersToAutoUpdate(DateTime lastUpdateThreshold, DateTime lastUpdateThresholdActiveUser);
        Task SetRatingRefreshResult(string imdbUserId, bool succeeded, string result);
        Task SetWatchlistRefreshResult(string imdbUserId, bool succeeded, string result);
        Task UnsetRefreshRequestTime(string imdbUserId);
    }

    public class UsersRepository : IUsersRepository
    {
        private readonly ILogger<UsersRepository> logger;
        private readonly FxMoviesDbContext fxMoviesDbContext;

        public UsersRepository(
            ILogger<UsersRepository> logger,
            FxMoviesDbContext fxMoviesDbContext)
        {
            this.logger = logger;
            this.fxMoviesDbContext = fxMoviesDbContext;
        }

        public IAsyncEnumerable<string> GetAllImdbUserIds()
        {
            return fxMoviesDbContext.Users
                .Select(u => u.ImdbUserId).AsAsyncEnumerable();
        }

        public IAsyncEnumerable<User> GetAllImdbUsersToAutoUpdate(DateTime lastUpdateThreshold, DateTime lastUpdateThresholdActiveUser)
        {
            return fxMoviesDbContext.Users
                .Where (u => 
                    u.RefreshRequestTime.HasValue  // requested to be refreshed, OR
                    || !u.LastRefreshRatingsTime.HasValue  // never refreshed before, OR
                    || u.LastRefreshRatingsTime.Value < lastUpdateThreshold  // last refresh is before inactive user threshold
                    || (u.LastUsageTime.HasValue && u.LastUsageTime.Value > u.LastRefreshRatingsTime.Value  // used since last refreshtime
                        && u.LastRefreshRatingsTime.Value < lastUpdateThresholdActiveUser)) // last refresh is before active user threshold
                .AsAsyncEnumerable();
        }

        public async Task SetRatingRefreshResult(string imdbUserId, bool succeeded, string result)
        {
            var user = await fxMoviesDbContext.Users.SingleOrDefaultAsync(u => u.ImdbUserId == imdbUserId);
            if (user != null)
            {
                user.LastRefreshSuccess = succeeded;
                user.LastRefreshRatingsTime = DateTime.UtcNow;
                user.LastRefreshRatingsResult = result;
                await fxMoviesDbContext.SaveChangesAsync();
            }
        }

        public async Task SetWatchlistRefreshResult(string imdbUserId, bool succeeded, string result)
        {
            var user = await fxMoviesDbContext.Users.SingleOrDefaultAsync(u => u.ImdbUserId == imdbUserId);
            if (user != null)
            {
                user.WatchListLastRefreshSuccess = succeeded;
                user.WatchListLastRefreshTime = DateTime.UtcNow;
                user.WatchListLastRefreshResult = result;
                await fxMoviesDbContext.SaveChangesAsync();
            }
        }

        public async Task UnsetRefreshRequestTime(string imdbUserId)
        {
            var user = await fxMoviesDbContext.Users.SingleOrDefaultAsync(u => u.ImdbUserId == imdbUserId);
            if (user != null)
            {
                user.RefreshRequestTime = null;
                await fxMoviesDbContext.SaveChangesAsync();
            }
        }
    }
}