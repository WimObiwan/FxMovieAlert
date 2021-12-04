using System;
using System.Collections.Generic;

namespace FxMovies.Core.Entities;

/// <summary>
/// A simple class representing a User
/// </summary>
public class User
{
    public User()
    {
    }

    public int Id { get; set; }
    public string UserId { get; set; }
    public string ImdbUserId { get; set; }
    public DateTime? RefreshRequestTime { get; set; }
    public DateTime? LastRefreshRatingsTime { get; set; }
    public bool? LastRefreshSuccess { get; set; }
    public string LastRefreshRatingsResult { get; set; }
    public long RefreshCount { get; set; }
    public DateTime? LastUsageTime { get; set; }
    public long Usages { get; set; }
    public DateTime? WatchListLastRefreshTime { get; set; }
    public bool? WatchListLastRefreshSuccess { get; set; }
    public string WatchListLastRefreshResult { get; set; }

    public List<UserRating> UserRatings { get; set; }
    public List<UserWatchListItem> UserWatchListItems { get; set; }
}