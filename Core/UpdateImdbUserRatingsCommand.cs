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
        Task<int> Run(string ImdbUserId);
    }

    public class UpdateImdbUserRatingsCommand : IUpdateImdbUserRatingsCommand
    {
        private readonly ILogger<UpdateImdbUserRatingsCommand> logger;
        private readonly IImdbRatingsService imdbRatingsService;
        private readonly IUserRatingsRepository userRatingsRepository;

        public UpdateImdbUserRatingsCommand(ILogger<UpdateImdbUserRatingsCommand> logger, 
            IImdbRatingsService imdbRatingsService,
            IUserRatingsRepository userRatingsRepository)
        {
            this.logger = logger;
            this.imdbRatingsService = imdbRatingsService;
            this.userRatingsRepository = userRatingsRepository;
        }

        public async Task<int> Run(string imdbUserId)
        {
            var ratings = await imdbRatingsService.GetRatingsAsync(imdbUserId);
            await userRatingsRepository.Store(imdbUserId, ratings, false);
            return 0;
        }
   }
}