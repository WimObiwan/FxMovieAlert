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
    public interface IUpdateImdbUserRatingsCommand
    {
        Task<int> Run(string ImdbUserId, bool updateAllRatings);
    }

    public class UpdateImdbUserRatingsCommand : IUpdateImdbUserRatingsCommand
    {
        private readonly ILogger<UpdateImdbUserRatingsCommand> logger;
        private readonly IImdbRatingsService imdbRatingsService;
        private readonly IUserRatingsRepository userRatingsRepository;
        private readonly IUsersRepository usersRepository;

        public UpdateImdbUserRatingsCommand(ILogger<UpdateImdbUserRatingsCommand> logger, 
            IImdbRatingsService imdbRatingsService,
            IUserRatingsRepository userRatingsRepository,
            IUsersRepository usersRepository)
        {
            this.logger = logger;
            this.imdbRatingsService = imdbRatingsService;
            this.userRatingsRepository = userRatingsRepository;
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
            return 0;
        }
   }
}