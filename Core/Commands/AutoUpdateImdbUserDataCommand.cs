using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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

[ExcludeFromCodeCoverage]
public class AutoUpdateImdbUserDataCommandOptions
{
    public static string Position => "AutoUpdateImdbUserData";

    public TimeSpan? AutoUpdateInterval { get; set; }
    public TimeSpan? AutoUpdateIntervalActiveUser { get; set; }
    public bool? UpdateAllRatings { get; set; }
}

public class AutoUpdateImdbUserDataCommand : IAutoUpdateImdbUserDataCommand
{
    private readonly TimeSpan _autoUpdateInterval;
    private readonly TimeSpan _autoUpdateIntervalActiveUser;
    private readonly ILogger<AutoUpdateImdbUserDataCommand> _logger;
    private readonly bool _updateAllRatings;
    private readonly IUpdateImdbUserDataCommand _updateImdbUserDataCommand;
    private readonly IUsersRepository _usersRepository;

    public AutoUpdateImdbUserDataCommand(ILogger<AutoUpdateImdbUserDataCommand> logger,
        IOptionsSnapshot<AutoUpdateImdbUserDataCommandOptions> autoUpdateImdbUserDataCommandOptions,
        IUpdateImdbUserDataCommand updateImdbUserDataCommand,
        IUsersRepository usersRepository)
    {
        _logger = logger;
        _updateImdbUserDataCommand = updateImdbUserDataCommand;
        _usersRepository = usersRepository;
        _autoUpdateInterval = autoUpdateImdbUserDataCommandOptions.Value.AutoUpdateInterval ?? TimeSpan.FromDays(1);
        _autoUpdateIntervalActiveUser = autoUpdateImdbUserDataCommandOptions.Value.AutoUpdateIntervalActiveUser ??
                                        TimeSpan.FromHours(1);
        _updateAllRatings = autoUpdateImdbUserDataCommandOptions.Value.UpdateAllRatings ?? false;
    }

    public async Task<int> Execute()
    {
        var now = DateTime.UtcNow;
        var lastUpdateThreshold = now.Add(-_autoUpdateInterval);
        var lastUpdateThresholdActiveUser = now.Add(-_autoUpdateIntervalActiveUser);

        _logger.LogInformation(
            "Loading users that need to be refreshed (inactive user threshold {LastUpdateThreshold}, active user threshold {LastUpdateThresholdActiveUser})",
            lastUpdateThreshold, lastUpdateThresholdActiveUser);

        // ToList, to prevent locking errors when updating while iterating:
        var usersToUpdate = new List<User>();
        await foreach (var item in _usersRepository.GetAllImdbUsersToAutoUpdate(lastUpdateThreshold,
                           lastUpdateThresholdActiveUser))
            usersToUpdate.Add(item);

        foreach (var user in usersToUpdate)
        {
            if (!string.IsNullOrEmpty(user.ImdbUserId))
            {
                _logger.LogInformation(
                    "User {ImdbUserId} needs a refresh of the IMDb User ratings, LastUsageTime = {LastUsageTime}",
                    user.ImdbUserId, user.LastUsageTime);
                if (user.RefreshRequestTime.HasValue)
                    _logger.LogInformation(
                        "   * Refresh requested (RefreshRequestTime {RefreshRequestTime}, {RefreshRequestTimeSecondsAgo} seconds ago)",
                        user.RefreshRequestTime.Value, (now - user.RefreshRequestTime.Value).TotalSeconds);
                if (!user.LastRefreshRatingsTime.HasValue)
                    _logger.LogInformation("   * Never refreshed");
                else if (user.LastRefreshRatingsTime.Value < lastUpdateThreshold)
                    _logger.LogInformation(
                        "   * Last refresh too old for inactive user, LastRefreshRatingsTime = {LastRefreshRatingsTime}",
                        user.LastRefreshRatingsTime.Value);
                else if (user.LastUsageTime.HasValue && user.LastUsageTime.Value >
                                                    user.LastRefreshRatingsTime.Value // used since last refreshtime
                                                    && user.LastRefreshRatingsTime.Value <
                                                    lastUpdateThresholdActiveUser) // last refresh is before active user threshold
                    _logger.LogInformation(
                        "   * Last refresh too old for active user, LastRefreshRatingsTime = {LastRefreshRatingsTime}",
                        user.LastRefreshRatingsTime.Value);
                try
                {
                    await _updateImdbUserDataCommand.Execute(user.ImdbUserId, _updateAllRatings);
                }
                catch (Exception x)
                {
                    _logger.LogError(x, "Failed to update ratings for ImdbUserId {ImdbUserId}", user.ImdbUserId);
                }
            }
            else
            {
                _logger.LogInformation(
                    "Skipping user {UserId}, because no ImdbUserId configured",
                    user.UserId);
            }
        }

        return 0;
    }
}