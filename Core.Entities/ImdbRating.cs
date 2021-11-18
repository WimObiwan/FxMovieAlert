using System;

namespace FxMovies.Core.Entities;

public class ImdbRating
{
    public string ImdbId { get; set; }
    public int Rating { get; set; }
    public DateTime Date { get; set; }
    public string Title { get; set; }
}
