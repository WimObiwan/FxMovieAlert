using System;

namespace FxMovies.Core.Entities;

/// <summary>
/// A simple class representing a MovieEvent
/// </summary>
public class ManualMatch
{
    public ManualMatch()
    {
    }

    public int Id { get; set; }
    public DateTime AddedDateTime { get; set; }
    public string Title { get; set; }
    public string NormalizedTitle { get; set; }
    public Movie Movie { get; set; }

    public int? MovieId { get; set; }
}