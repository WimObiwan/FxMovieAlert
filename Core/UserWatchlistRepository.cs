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
    public class UserWatchlistRepositoryStoreResult
    {
        public int ExistingCount { get; internal set; }
        public int NewCount { get; internal set; }
        public int RemovedCount { get; internal set; }
        public string LastTitle { get; internal set; }
    }

    public interface IUserWatchlistRepository
    {
        Task<UserWatchlistRepositoryStoreResult> Store(string imdbUserId, IEnumerable<ImdbWatchlist> watchlistEntries, bool replace = false);
    }

    public class UserWatchlistRepository : IUserWatchlistRepository
    {
        private readonly ILogger<UserWatchlistRepository> logger;
        private readonly FxMoviesDbContext fxMoviesDbContext;
        private readonly IMovieCreationHelper movieCreationHelper;

        public UserWatchlistRepository(
            ILogger<UserWatchlistRepository> logger,
            FxMoviesDbContext fxMoviesDbContext,
            IMovieCreationHelper movieCreationHelper)
        {
            this.logger = logger;
            this.fxMoviesDbContext = fxMoviesDbContext;
            this.movieCreationHelper = movieCreationHelper;
        }

        public async Task<UserWatchlistRepositoryStoreResult> Store(string imdbUserId, IEnumerable<ImdbWatchlist> imdbWatchlistEntries, bool replace = false)
        {
            User user = await fxMoviesDbContext.Users.FirstOrDefaultAsync(u => u.ImdbUserId == imdbUserId);
            int newCount = 0, existingCount = 0;
            List<string> movieIdsInData = new List<string>();
            string lastTitle = null;
            foreach (var imdbWatchlistEntry in imdbWatchlistEntries)
            {
                if (lastTitle == null)
                    lastTitle = imdbWatchlistEntry.Title;
                var imdbId = imdbWatchlistEntry.ImdbId;
                movieIdsInData.Add(imdbId);
                var movie = await movieCreationHelper.GetOrCreateMovieByImdbId(imdbId);
                var userWatchlistItem = fxMoviesDbContext.UserWatchLists.FirstOrDefault(ur => ur.User == user && ur.Movie == movie);
                if (userWatchlistItem == null)
                {
                    userWatchlistItem = new UserWatchListItem();
                    userWatchlistItem.User = user;
                    userWatchlistItem.Movie = movie;
                    fxMoviesDbContext.UserWatchLists.Add(userWatchlistItem);
                    newCount++;
                }
                else
                {
                    existingCount++;
                }
                userWatchlistItem.AddedDate = imdbWatchlistEntry.Date;
            }

            int removedCount;
            if (replace)
            {
                List<UserWatchListItem> itemsToRemove = 
                    await fxMoviesDbContext.UserWatchLists
                        .Where(ur => ur.UserId == user.Id && !movieIdsInData.Contains(ur.Movie.ImdbId))
                        .ToListAsync();

                fxMoviesDbContext.UserWatchLists.RemoveRange(itemsToRemove);
                removedCount = itemsToRemove.Count;
            }
            else
            {
                removedCount = 0;
            }

            await fxMoviesDbContext.SaveChangesAsync();

            return new UserWatchlistRepositoryStoreResult()
            {
                ExistingCount = existingCount,
                NewCount = newCount,
                RemovedCount = removedCount,
                LastTitle = lastTitle
            };
        }

    }
}