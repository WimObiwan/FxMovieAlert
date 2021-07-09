using System;
using System.Threading.Tasks;
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
        public TimeSpan? AutoUpdateIntervalActiveUser { get; set; }
        public bool? UpdateAllRatings { get; set; }
    }

    public class AutoUpdateImdbUserDataCommand : IAutoUpdateImdbUserDataCommand
    {
        private readonly ILogger<AutoUpdateImdbUserDataCommand> logger;
        private readonly IUpdateImdbUserDataCommand updateImdbUserDataCommand;
        private readonly IUsersRepository usersRepository;
        private readonly TimeSpan autoUpdateInterval;
        private readonly TimeSpan autoUpdateIntervalActiveUser;
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
            this.autoUpdateIntervalActiveUser = autoUpdateImdbUserDataCommandOptions.Value.AutoUpdateIntervalActiveUser ?? TimeSpan.FromHours(1);
            this.updateAllRatings = autoUpdateImdbUserDataCommandOptions.Value.UpdateAllRatings ?? false;
        }

        public async Task<int> Run()
        {
            var now = DateTime.UtcNow;
            var lastUpdateThreshold = now.Add(-autoUpdateInterval);
            var lastUpdateThresholdActiveUser = now.Add(-autoUpdateIntervalActiveUser);

            logger.LogInformation($"Loading users that need to be refreshed (inactive user threshold {lastUpdateThreshold}, active user threshold {lastUpdateThresholdActiveUser})");

            await foreach (var user in usersRepository.GetAllImdbUsersToAutoUpdate(lastUpdateThreshold, lastUpdateThresholdActiveUser))
            {
                logger.LogInformation($"User {user.ImdbUserId} needs a refresh of the IMDb User ratings, LastUsageTime = {user.LastUsageTime}");
                if (user.RefreshRequestTime.HasValue)
                    logger.LogInformation($"   * Refresh requested (RefreshRequestTime {user.RefreshRequestTime.Value}, {(now - user.RefreshRequestTime.Value).TotalSeconds} seconds ago)");
                if (!user.LastRefreshRatingsTime.HasValue)
                    logger.LogInformation("   * Never refreshed");
                else if (user.LastRefreshRatingsTime.Value < lastUpdateThreshold)
                    logger.LogInformation($"   * Last refresh too old for inactive user, LastRefreshRatingsTime = {user.LastRefreshRatingsTime.Value}");
                else if (user.LastUsageTime.HasValue && user.LastUsageTime.Value > user.LastRefreshRatingsTime.Value  // used since last refreshtime
                        && user.LastRefreshRatingsTime.Value < lastUpdateThresholdActiveUser) // last refresh is before active user threshold
                    logger.LogInformation($"   * Last refresh too old for active user, LastRefreshRatingsTime = {user.LastRefreshRatingsTime.Value}");
                    
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