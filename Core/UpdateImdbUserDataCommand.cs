using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace FxMovies.Core
{
    public interface IUpdateImdbUserDataCommand
    {
        Task<int> Run(string ImdbUserId, bool updateAllRatings);
    }

    public class UpdateImdbUserDataCommand : IUpdateImdbUserDataCommand
    {
        private readonly ILogger<UpdateImdbUserDataCommand> logger;
        private readonly IImdbRatingsFromWebService imdbRatingsService;
        private readonly IImdbWatchlistService imdbWatchlistService;
        private readonly IUserRatingsRepository userRatingsRepository;
        private readonly IUserWatchlistRepository userWatchlistRepository;
        private readonly IUsersRepository usersRepository;

        public UpdateImdbUserDataCommand(ILogger<UpdateImdbUserDataCommand> logger, 
            IImdbRatingsFromWebService imdbRatingsService,
            IImdbWatchlistService imdbWatchlistService,
            IUserRatingsRepository userRatingsRepository,
            IUserWatchlistRepository userWatchlistRepository,
            IUsersRepository usersRepository)
        {
            this.logger = logger;
            this.imdbRatingsService = imdbRatingsService;
            this.imdbWatchlistService = imdbWatchlistService;
            this.userRatingsRepository = userRatingsRepository;
            this.userWatchlistRepository = userWatchlistRepository;
            this.usersRepository = usersRepository;
        }

        public async Task<int> Run(string imdbUserId, bool updateAllRatings)
        {
            try
            {
                var ratings = await imdbRatingsService.GetRatingsAsync(imdbUserId, updateAllRatings);
                var result = await userRatingsRepository.StoreByImdbUserId(imdbUserId, ratings, updateAllRatings);
                string message = $"{result.NewCount} nieuwe en {result.ExistingCount} bestaande films.";
                if (updateAllRatings)
                    message = $"  {result.RemovedCount} films verwijderd.";
                await usersRepository.SetRatingRefreshResult(imdbUserId, true, message);
            }
            catch (Exception x)
            {
                await usersRepository.SetRatingRefreshResult(imdbUserId, false, x.Message);
                throw;
            }

            try
            {
                var watchlistEntries = await imdbWatchlistService.GetWatchlistAsync(imdbUserId);
                var result = await userWatchlistRepository.Store(imdbUserId, watchlistEntries, true);
                string message = $"{result.NewCount} nieuwe en {result.ExistingCount} bestaande films."
                    + $"  {result.RemovedCount} films verwijderd.";
                await usersRepository.SetWatchlistRefreshResult(imdbUserId, true, message);
            }
            catch (Exception x)
            {
                await usersRepository.SetWatchlistRefreshResult(imdbUserId, false, x.Message);
                throw;
            }

            await usersRepository.UnsetRefreshRequestTime(imdbUserId);

            return 0;
        }
   }
}