using System.Collections.Generic;

namespace FxMovies.Core.Entities;

/// <summary>
///     A simple class representing a User
/// </summary>
public class Movie
{
    public int Id { get; set; }
    public string? ImdbId { get; set; }
    public int? ImdbRating { get; set; }
    public int? ImdbVotes { get; set; }
    public string? Certification { get; set; }
    public string? OriginalTitle { get; set; }
    public bool ImdbIgnore { get; set; }

    public List<MovieEvent> MovieEvents { get; set; } = default!;
    public List<UserRating> UserRatings { get; set; } = default!;
    public List<UserWatchListItem> UserWatchListItems { get; set; } = default!;
    public List<ManualMatch> ManualMatches { get; set; } = default!;
}