using System;

namespace FxMovies.Core.Entities;

public class ImdbWatchlist
{
    public string? ImdbId { get; set; }
    public DateTime? Date { get; set; }
    public string? Title { get; set; }
}