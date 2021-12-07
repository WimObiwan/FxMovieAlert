using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FxMovies.Core.Repositories;
using FxMovies.Core.Services;

namespace FxMovies.Core.Commands;

public interface IUpdateImdbUserDataCommand
{
    Task<int> Execute(string imdbUserId, bool updateAllRatings);
}

public class UpdateImdbUserDataCommand : IUpdateImdbUserDataCommand
{
    private readonly IImdbRatingsFromWebService _imdbRatingsService;
    private readonly IImdbWatchlistFromWebService _imdbWatchlistService;
    private readonly IUserRatingsRepository _userRatingsRepository;
    private readonly IUsersRepository _usersRepository;
    private readonly IUserWatchlistRepository _userWatchlistRepository;

    public UpdateImdbUserDataCommand(
        IImdbRatingsFromWebService imdbRatingsService,
        IImdbWatchlistFromWebService imdbWatchlistService,
        IUserRatingsRepository userRatingsRepository,
        IUserWatchlistRepository userWatchlistRepository,
        IUsersRepository usersRepository)
    {
        _imdbRatingsService = imdbRatingsService;
        _imdbWatchlistService = imdbWatchlistService;
        _userRatingsRepository = userRatingsRepository;
        _userWatchlistRepository = userWatchlistRepository;
        _usersRepository = usersRepository;
    }

    public async Task<int> Execute(string imdbUserId, bool updateAllRatings)
    {
        try
        {
            DateTime? fromDateTime;
            if (updateAllRatings)
                fromDateTime = DateTime.MinValue;
            else
                fromDateTime = await _userRatingsRepository.GetLastRatingCheckByImdbUserId(imdbUserId) ??
                               DateTime.MinValue;

            var ratings = await _imdbRatingsService.GetRatingsAsync(imdbUserId, fromDateTime);
            var result = await _userRatingsRepository.StoreByImdbUserId(imdbUserId, ratings, updateAllRatings);
            var message = $"{result.NewCount} nieuwe en {result.ExistingCount} bestaande films.";
            if (updateAllRatings)
                message = $"  {result.RemovedCount} films verwijderd.";
            await _usersRepository.SetRatingRefreshResult(imdbUserId, true, message);
        }
        catch (Exception x)
        {
            await _usersRepository.SetRatingRefreshResult(imdbUserId, false, x.Message);
            if (x is HttpRequestException x2 && x2.StatusCode != HttpStatusCode.Forbidden)
                throw;
        }

        try
        {
            var watchlistEntries = await _imdbWatchlistService.GetWatchlistAsync(imdbUserId);
            var result = await _userWatchlistRepository.StoreByImdbUserId(imdbUserId, watchlistEntries, true);
            var message = $"{result.NewCount} nieuwe en {result.ExistingCount} bestaande films."
                          + $"  {result.RemovedCount} films verwijderd.";
            await _usersRepository.SetWatchlistRefreshResult(imdbUserId, true, message);
        }
        catch (Exception x)
        {
            await _usersRepository.SetWatchlistRefreshResult(imdbUserId, false, x.Message);
            if (x is HttpRequestException x2 && x2.StatusCode != HttpStatusCode.Forbidden)
                throw;
        }

        await _usersRepository.UnsetRefreshRequestTime(imdbUserId);

        return 0;
    }
}