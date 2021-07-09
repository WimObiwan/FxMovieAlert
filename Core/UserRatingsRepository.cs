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
    public class UserRatingsRepositoryStoreResult
    {
        public int ExistingCount { get; internal set; }
        public int NewCount { get; internal set; }
        public int RemovedCount { get; internal set; }
        public string LastTitle { get; internal set; }
    }

    public interface IUserRatingsRepository
    {
        Task<UserRatingsRepositoryStoreResult> StoreByImdbUserId(string imdbUserId, IEnumerable<ImdbRating> imdbRatings, bool replace = false);
        Task<UserRatingsRepositoryStoreResult> StoreByUserId(string userId, IEnumerable<ImdbRating> imdbRatings, bool replace = false);
    }

    public class UserRatingsRepository : IUserRatingsRepository
    {
        private readonly ILogger<UserRatingsRepository> logger;
        private readonly FxMoviesDbContext fxMoviesDbContext;
        private readonly IMovieCreationHelper movieCreationHelper;

        public UserRatingsRepository(
            ILogger<UserRatingsRepository> logger,
            FxMoviesDbContext fxMoviesDbContext,
            IMovieCreationHelper movieCreationHelper)
        {
            this.logger = logger;
            this.fxMoviesDbContext = fxMoviesDbContext;
            this.movieCreationHelper = movieCreationHelper;
        }

        public async Task<UserRatingsRepositoryStoreResult> StoreByImdbUserId(string imdbUserId, IEnumerable<ImdbRating> imdbRatings, bool replace = false)
        {
            User user = await fxMoviesDbContext.Users.FirstOrDefaultAsync(u => u.ImdbUserId == imdbUserId);
            return await Store(user, imdbRatings, replace);
        }

        public async Task<UserRatingsRepositoryStoreResult> StoreByUserId(string userId, IEnumerable<ImdbRating> imdbRatings, bool replace = false)
        {
            User user = await fxMoviesDbContext.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            return await Store(user, imdbRatings, replace);
        }

        private async Task<UserRatingsRepositoryStoreResult> Store(User user, IEnumerable<ImdbRating> imdbRatings, bool replace)
        {
            int newCount = 0, existingCount = 0;
            List<string> movieIdsInData = new List<string>();
            string lastTitle = null;
            foreach (var imdbRating in imdbRatings)
            {
                if (lastTitle == null)
                    lastTitle = imdbRating.Title;
                var imdbId = imdbRating.ImdbId;
                movieIdsInData.Add(imdbId);
                var movie = await movieCreationHelper.GetOrCreateMovieByImdbId(imdbId);
                var userRating = fxMoviesDbContext.UserRatings.FirstOrDefault(ur => ur.User == user && ur.Movie == movie);
                if (userRating == null)
                {
                    userRating = new UserRating();
                    userRating.User = user;
                    userRating.Movie = movie;
                    fxMoviesDbContext.UserRatings.Add(userRating);
                    newCount++;
                }
                else
                {
                    existingCount++;
                }
                userRating.Rating = imdbRating.Rating;
                userRating.RatingDate = imdbRating.Date;
            }

            int removedCount;
            if (replace)
            {
                List<UserRating> itemsToRemove = 
                    await fxMoviesDbContext.UserRatings
                        .Where(ur => ur.UserId == user.Id && !movieIdsInData.Contains(ur.Movie.ImdbId))
                        .ToListAsync();

                fxMoviesDbContext.UserRatings.RemoveRange(itemsToRemove);
                removedCount = itemsToRemove.Count;
            }
            else
            {
                removedCount = 0;
            }

            await fxMoviesDbContext.SaveChangesAsync();

            return new UserRatingsRepositoryStoreResult()
            {
                ExistingCount = existingCount,
                NewCount = newCount,
                RemovedCount = removedCount,
                LastTitle = lastTitle
            };
        }

    }
}