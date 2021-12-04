using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FxMovies.Core.Entities;
using FxMovies.Core.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FxMovies.Core.Commands;

public interface IAutoUpdateImdbUserDataCommand
{
    Task<int> Execute();
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
    private readonly TimeSpan autoUpdateInterval;
    private readonly TimeSpan autoUpdateIntervalActiveUser;
    private readonly ILogger<AutoUpdateImdbUserDataCommand> logger;
    private readonly bool updateAllRatings;
    private readonly IUpdateImdbUserDataCommand updateImdbUserDataCommand;
    private readonly IUsersRepository usersRepository;

    public AutoUpdateImdbUserDataCommand(ILogger<AutoUpdateImdbUserDataCommand> logger,
        IOptionsSnapshot<AutoUpdateImdbUserDataCommandOptions> autoUpdateImdbUserDataCommandOptions,
        IUpdateImdbUserDataCommand updateImdbUserDataCommand,
        IUsersRepository usersRepository)
    {
        this.logger = logger;
        this.updateImdbUserDataCommand = updateImdbUserDataCommand;
        this.usersRepository = usersRepository;
        autoUpdateInterval = autoUpdateImdbUserDataCommandOptions.Value.AutoUpdateInterval ?? TimeSpan.FromDays(1);
        autoUpdateIntervalActiveUser = autoUpdateImdbUserDataCommandOptions.Value.AutoUpdateIntervalActiveUser ??
                                       TimeSpan.FromHours(1);
        updateAllRatings = autoUpdateImdbUserDataCommandOptions.Value.UpdateAllRatings ?? false;
    }

    public async Task<int> Execute()
    {
        var now = DateTime.UtcNow;
        var lastUpdateThreshold = now.Add(-autoUpdateInterval);
        var lastUpdateThresholdActiveUser = now.Add(-autoUpdateIntervalActiveUser);

        logger.LogInformation(
            "Loading users that need to be refreshed (inactive user threshold {LastUpdateThreshold}, active user threshold {LastUpdateThresholdActiveUser})",
            lastUpdateThreshold, lastUpdateThresholdActiveUser);

        // ToList, to prevent locking errors when updating while iterating:
        var usersToUpdate = new List<User>();
        await foreach (var item in usersRepository.GetAllImdbUsersToAutoUpdate(lastUpdateThreshold,
                           lastUpdateThresholdActiveUser))
            usersToUpdate.Add(item);

        foreach (var user in usersToUpdate)
        {
            logger.LogInformation(
                "User {ImdbUserId} needs a refresh of the IMDb User ratings, LastUsageTime = {LastUsageTime}",
                user.ImdbUserId, user.LastUsageTime);
            if (user.RefreshRequestTime.HasValue)
                logger.LogInformation(
                    "   * Refresh requested (RefreshRequestTime {RefreshRequestTime}, {RefreshRequestTimeSecondsAgo} seconds ago)",
                    user.RefreshRequestTime.Value, (now - user.RefreshRequestTime.Value).TotalSeconds);
            if (!user.LastRefreshRatingsTime.HasValue)
                logger.LogInformation("   * Never refreshed");
            else if (user.LastRefreshRatingsTime.Value < lastUpdateThreshold)
                logger.LogInformation(
                    "   * Last refresh too old for inactive user, LastRefreshRatingsTime = {LastRefreshRatingsTime}",
                    user.LastRefreshRatingsTime.Value);
            else if (user.LastUsageTime.HasValue && user.LastUsageTime.Value >
                                                 user.LastRefreshRatingsTime.Value // used since last refreshtime
                                                 && user.LastRefreshRatingsTime.Value <
                                                 lastUpdateThresholdActiveUser) // last refresh is before active user threshold
                logger.LogInformation(
                    "   * Last refresh too old for active user, LastRefreshRatingsTime = {LastRefreshRatingsTime}",
                    user.LastRefreshRatingsTime.Value);
            try
            {
                await updateImdbUserDataCommand.Execute(user.ImdbUserId, updateAllRatings);
            }
            catch (Exception x)
            {
                logger.LogError(x, "Failed to update ratings for ImdbUserId {ImdbUserId}", user.ImdbUserId);
            }
        }

        return 0;
    }
}