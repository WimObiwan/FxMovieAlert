using System.Collections.Generic;

namespace FxMovies.FxMoviesDB
{
    /// <summary>
    /// A simple class representing a User
    /// </summary>
    public class Movie
    {
        public Movie()
        {
        }

        public int Id { get; set; }
        public string ImdbId { get; set; }
        public int? ImdbRating { get; set; }
        public int? ImdbVotes { get; set; }
        public string Certification { get; set; }

        public List<MovieEvent> MovieEvents { get; set; }
    }   
}