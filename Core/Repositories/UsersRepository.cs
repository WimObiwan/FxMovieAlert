using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FxMovies.Core.Entities;
using FxMovies.FxMoviesDB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FxMovies.Core.Repositories
{
    public class UserDataResult
    {
        public DateTime? RefreshRequestTime { get; set; }
        public DateTime? LastRefreshRatingsTime { get; set; }
        public bool? LastRefreshSuccess { get; set; }
        public string ImdbUserId { get; set; }
    }

    public interface IUsersRepository
    {
        IAsyncEnumerable<string> GetAllImdbUserIds();
        IAsyncEnumerable<User> GetAllImdbUsersToAutoUpdate(DateTime lastUpdateThreshold, DateTime lastUpdateThresholdActiveUser);
        Task SetRatingRefreshResult(string imdbUserId, bool succeeded, string result);
        Task SetWatchlistRefreshResult(string imdbUserId, bool succeeded, string result);
        Task UnsetRefreshRequestTime(string imdbUserId);
        Task<UserDataResult> UpdateUserLastUsedAndGetData(string userId);
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

        public async Task<UserDataResult> UpdateUserLastUsedAndGetData(string userId)
        {
            var user = await fxMoviesDbContext.Users.Where(u => u.UserId == userId).SingleOrDefaultAsync();
            if (user == null)
                return null;
                
            user.Usages++;
            user.LastUsageTime = DateTime.UtcNow;

            // "SQLite Error 8: 'attempt to write a readonly database'."... 
            await fxMoviesDbContext.SaveChangesAsync();

            return new UserDataResult()
            {
                RefreshRequestTime = user.RefreshRequestTime,
                LastRefreshRatingsTime = user.LastRefreshRatingsTime,
                LastRefreshSuccess = user.LastRefreshSuccess,
                ImdbUserId = user.ImdbUserId
            };
        }
    }
}