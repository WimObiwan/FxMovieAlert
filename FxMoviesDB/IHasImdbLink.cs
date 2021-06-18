using System;

namespace FxMovies.FxMoviesDB
{
    public interface IHasImdbLink
    {
        string Title { get; }
        int? Year { get; }
        Movie Movie { get; set; }
    }
}