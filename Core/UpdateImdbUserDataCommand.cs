using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FxMovies.FxMoviesDB;
using FxMovies.ImdbDB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FxMovies.Core
{
    public interface IUpdateImdbUserDataCommand
    {
        Task<int> Run(string ImdbUserId, bool updateAllRatings);
    }

    public class UpdateImdbUserDataCommand : IUpdateImdbUserDataCommand
    {
        private readonly ILogger<UpdateImdbUserDataCommand> logger;
        private readonly IImdbRatingsService imdbRatingsService;
        private readonly IImdbWatchlistService imdbWatchlistService;
        private readonly IUserRatingsRepository userRatingsRepository;
        private readonly IUserWatchlistRepository userWatchlistRepository;
        private readonly IUsersRepository usersRepository;

        public UpdateImdbUserDataCommand(ILogger<UpdateImdbUserDataCommand> logger, 
            IImdbRatingsService imdbRatingsService,
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
                var result = await userRatingsRepository.Store(imdbUserId, ratings, updateAllRatings);
                string message = $"{result.NewCount} nieuwe films.  Laatste film is {result.LastTitle}.";
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
                string message = $"{result.NewCount} nieuwe films.  Laatste film is {result.LastTitle}.";
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