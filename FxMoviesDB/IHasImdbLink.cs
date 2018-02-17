using System;

namespace FxMovies.FxMoviesDB
{
    public interface IHasImdbLink
    {
        string Title { get; }
        int? Year { get; }
        string ImdbId { get; set; }
        int? ImdbRating { get; set; }
        int? ImdbVotes { get; set; }
        string Certification { get; set; }
    }
}