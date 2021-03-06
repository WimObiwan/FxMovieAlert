using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FxMovies.FxMoviesDB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FxMovies.Core
{
    public interface IUserRatingsRepository
    {
        Task<UserListRepositoryStoreResult> StoreByImdbUserId(string imdbUserId, IEnumerable<ImdbRating> imdbRatings, bool replace = false);
        Task<UserListRepositoryStoreResult> StoreByUserId(string userId, IEnumerable<ImdbRating> imdbRatings, bool replace = false);
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

        public async Task<UserListRepositoryStoreResult> StoreByImdbUserId(string imdbUserId, IEnumerable<ImdbRating> imdbRatings, bool replace = false)
        {
            User user = await fxMoviesDbContext.Users.FirstOrDefaultAsync(u => u.ImdbUserId == imdbUserId);
            return await Store(user, imdbRatings, replace);
        }

        public async Task<UserListRepositoryStoreResult> StoreByUserId(string userId, IEnumerable<ImdbRating> imdbRatings, bool replace = false)
        {
            User user = await fxMoviesDbContext.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            return await Store(user, imdbRatings, replace);
        }

        private async Task<UserListRepositoryStoreResult> Store(User user, IEnumerable<ImdbRating> imdbRatings, bool replace)
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

            return new UserListRepositoryStoreResult()
            {
                ExistingCount = existingCount,
                NewCount = newCount,
                RemovedCount = removedCount,
                LastTitle = lastTitle
            };
        }

    }
}