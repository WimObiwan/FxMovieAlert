using System;

namespace FxMovies.Core.Entities;

/// <summary>
/// A simple class representing a MovieEvent
/// </summary>
public class MovieEvent : IHasImdbLink
{
    public enum FeedType
    {
        Broadcast,
        FreeVod,
        PaidVod
    }

    public MovieEvent()
    {
    }

    public int Id { get; set; }
    public string ExternalId { get; set; }
    public int? Type { get; set; } // 1 = movie, 2 = short movie, 3 = serie
    public string Title { get; set; }
    public int? Year { get; set; }
    public bool Vod { get; set; }
    public FeedType? Feed { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public Channel Channel { get; set; }
    public string PosterS { get; set; }
    public string PosterM { get; set; }
    public int? Duration { get; set; }
    public string Genre { get; set; }
    public string Content { get; set; }
    public string Opinion { get; set; }
    public Movie Movie { get; set; }
    public string YeloUrl { get; set; }
    public string PosterS_Local { get; set; }
    public string PosterM_Local { get; set; }
    public string VodLink { get; set; }
    public DateTime? AddedTime { get; set; }
}