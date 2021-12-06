using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FxMovies.Core.Entities;
using FxMovies.MoviesDB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FxMovies.Core.Repositories;

public interface IUserWatchlistRepository
{
    Task<UserListRepositoryStoreResult> StoreByImdbUserId(string imdbUserId, IEnumerable<ImdbWatchlist> imdbWatchlist,
        bool replace = false);

    Task<UserListRepositoryStoreResult> StoreByUserId(string userId, IEnumerable<ImdbWatchlist> imdbWatchlist,
        bool replace = false);
}

public class UserWatchlistRepository : IUserWatchlistRepository
{
    private readonly ILogger<UserWatchlistRepository> _logger;
    private readonly IMovieCreationHelper _movieCreationHelper;
    private readonly MoviesDbContext _moviesDbContext;

    public UserWatchlistRepository(
        ILogger<UserWatchlistRepository> logger,
        MoviesDbContext moviesDbContext,
        IMovieCreationHelper movieCreationHelper)
    {
        _logger = logger;
        _moviesDbContext = moviesDbContext;
        _movieCreationHelper = movieCreationHelper;
    }

    public async Task<UserListRepositoryStoreResult> StoreByImdbUserId(string imdbUserId,
        IEnumerable<ImdbWatchlist> imdbWatchlist, bool replace = false)
    {
        var user = await _moviesDbContext.Users.FirstOrDefaultAsync(u => u.ImdbUserId == imdbUserId);
        return await Store(user, imdbWatchlist, replace);
    }

    public async Task<UserListRepositoryStoreResult> StoreByUserId(string userId,
        IEnumerable<ImdbWatchlist> imdbWatchlist, bool replace = false)
    {
        var user = await _moviesDbContext.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        return await Store(user, imdbWatchlist, replace);
    }

    private async Task<UserListRepositoryStoreResult> Store(User user, IEnumerable<ImdbWatchlist> imdbWatchlist,
        bool replace)
    {
        int newCount = 0, existingCount = 0;
        var movieIdsInData = new List<string>();
        string lastTitle = null;
        foreach (var imdbWatchlistEntry in imdbWatchlist)
        {
            if (lastTitle == null)
                lastTitle = imdbWatchlistEntry.Title;
            var imdbId = imdbWatchlistEntry.ImdbId;
            movieIdsInData.Add(imdbId);
            var movie = await _movieCreationHelper.GetOrCreateMovieByImdbId(imdbId);
            var userWatchlistItem =
                _moviesDbContext.UserWatchLists.FirstOrDefault(ur => ur.User == user && ur.Movie == movie);
            if (userWatchlistItem == null)
            {
                userWatchlistItem = new UserWatchListItem();
                userWatchlistItem.User = user;
                userWatchlistItem.Movie = movie;
                _moviesDbContext.UserWatchLists.Add(userWatchlistItem);
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
            var itemsToRemove =
                await _moviesDbContext.UserWatchLists
                    .Where(ur => ur.UserId == user.Id && !movieIdsInData.Contains(ur.Movie.ImdbId))
                    .ToListAsync();

            _moviesDbContext.UserWatchLists.RemoveRange(itemsToRemove);
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