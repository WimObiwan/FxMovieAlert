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
    public interface IAutoUpdateImdbUserDataCommand
    {
        Task<int> Run();
    }

    public class AutoUpdateImdbUserDataCommandOptions
    {
        public static string Position => "AutoUpdateImdbUserData";

        public TimeSpan? AutoUpdateInterval { get; set; }
        public bool? UpdateAllRatings { get; set; }
    }

    public class AutoUpdateImdbUserDataCommand : IAutoUpdateImdbUserDataCommand
    {
        private readonly ILogger<AutoUpdateImdbUserDataCommand> logger;
        private readonly IUpdateImdbUserDataCommand updateImdbUserDataCommand;
        private readonly IUsersRepository usersRepository;
        private readonly TimeSpan autoUpdateInterval;
        private readonly bool updateAllRatings;

        public AutoUpdateImdbUserDataCommand(ILogger<AutoUpdateImdbUserDataCommand> logger,
            IOptionsSnapshot<AutoUpdateImdbUserDataCommandOptions> autoUpdateImdbUserDataCommandOptions,
            IUpdateImdbUserDataCommand updateImdbUserDataCommand,
            IUsersRepository usersRepository)
        {
            this.logger = logger;
            this.updateImdbUserDataCommand = updateImdbUserDataCommand;
            this.usersRepository = usersRepository;
            this.autoUpdateInterval = autoUpdateImdbUserDataCommandOptions.Value.AutoUpdateInterval ?? TimeSpan.FromDays(1);
            this.updateAllRatings = autoUpdateImdbUserDataCommandOptions.Value.UpdateAllRatings ?? false;
        }

        public async Task<int> Run()
        {
            var now = DateTime.UtcNow;
            var lastUpdateThreshold = now.Add(-autoUpdateInterval);

            logger.LogInformation($"Loading users that need to be refreshed (threshold {lastUpdateThreshold})");

            await foreach (var user in usersRepository.GetAllImdbUsersToAutoUpdate(lastUpdateThreshold))
            {
                logger.LogInformation($"User {user.ImdbUserId} needs a refresh of the IMDb User ratings");
                if (user.RefreshRequestTime.HasValue)
                    logger.LogInformation($"   * RefreshRequestTime = {user.RefreshRequestTime.Value} ({(now - user.RefreshRequestTime.Value).TotalSeconds} seconds ago)");
                if (!user.LastRefreshRatingsTime.HasValue)
                    logger.LogInformation("   * LastRefreshRatingsTime = null");
                else 
                    logger.LogInformation($"   * LastRefreshRatingsTime = {user.LastRefreshRatingsTime.Value}");
                    
                try
                {
                    await updateImdbUserDataCommand.Run(user.ImdbUserId, updateAllRatings);
                }
                catch (Exception x)
                {
                    logger.LogError(x, $"Failed to update ratings for ImdbUserId={user.ImdbUserId}");
                }
            }

            return 0;
        }
   }
}