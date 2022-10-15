using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FxMovies.Core.Entities;
using FxMovies.MoviesDB;
using Microsoft.EntityFrameworkCore;

namespace FxMovies.Core.Repositories;

public interface IUserRatingsRepository
{
    Task<DateTime?> GetLastRatingCheckByImdbUserId(string imdbUserId);

    Task<UserListRepositoryStoreResult> StoreByImdbUserId(string imdbUserId, IEnumerable<ImdbRating> imdbRatings,
        bool replace = false);

    Task<UserListRepositoryStoreResult> StoreByUserId(string userId, IEnumerable<ImdbRating> imdbRatings,
        bool replace = false);
}

public class UserRatingsRepository : IUserRatingsRepository
{
    private readonly IMovieCreationHelper _movieCreationHelper;
    private readonly MoviesDbContext _moviesDbContext;

    public UserRatingsRepository(
        MoviesDbContext moviesDbContext,
        IMovieCreationHelper movieCreationHelper)
    {
        _moviesDbContext = moviesDbContext;
        _movieCreationHelper = movieCreationHelper;
    }

    public async Task<DateTime?> GetLastRatingCheckByImdbUserId(string imdbUserId)
    {
        var user = await _moviesDbContext.Users.FirstOrDefaultAsync(u => u.ImdbUserId == imdbUserId);
        return user?.LastRefreshRatingsTime;
    }

    public async Task<UserListRepositoryStoreResult> StoreByImdbUserId(string imdbUserId,
        IEnumerable<ImdbRating> imdbRatings, bool replace = false)
    {
        var user = await _moviesDbContext.Users.SingleAsync(u => u.ImdbUserId == imdbUserId);
        return await Store(user, imdbRatings, replace);
    }

    public async Task<UserListRepositoryStoreResult> StoreByUserId(string userId, IEnumerable<ImdbRating> imdbRatings,
        bool replace = false)
    {
        var user = await _moviesDbContext.Users.SingleAsync(u => u.UserId == userId);
        return await Store(user, imdbRatings, replace);
    }

    private async Task<UserListRepositoryStoreResult> Store(User user, IEnumerable<ImdbRating> imdbRatings,
        bool replace)
    {
        int newCount = 0, existingCount = 0;
        var movieIdsInData = new List<string>();
        string? lastTitle = null;
        foreach (var imdbRating in imdbRatings)
        {
            lastTitle ??= imdbRating.Title;
            var imdbId = imdbRating.ImdbId;
            if (imdbId == null)
                continue;

            movieIdsInData.Add(imdbId);
            var movie = await _movieCreationHelper.GetOrCreateMovieByImdbId(imdbId);
            var userRating = _moviesDbContext.UserRatings.FirstOrDefault(ur => ur.User == user && ur.Movie == movie);
            if (userRating == null)
            {
                userRating = new UserRating
                {
                    User = user,
                    Movie = movie
                };
                _moviesDbContext.UserRatings.Add(userRating);
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
            var itemsToRemove =
                await _moviesDbContext.UserRatings
                    .Where(ur =>
                        ur.UserId == user.Id && ur.Movie != null && ur.Movie.ImdbId != null &&
                        !movieIdsInData.Contains(ur.Movie.ImdbId))
                    .ToListAsync();

            _moviesDbContext.UserRatings.RemoveRange(itemsToRemove);
            removedCount = itemsToRemove.Count;
        }
        else
        {
            removedCount = 0;
        }

        await _moviesDbContext.SaveChangesAsync();

        return new UserListRepositoryStoreResult
        {
            ExistingCount = existingCount,
            NewCount = newCount,
            RemovedCount = removedCount,
            LastTitle = lastTitle
        };
    }
}