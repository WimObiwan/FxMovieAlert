using System;
using System.ComponentModel.DataAnnotations;

namespace FxMovies.FxMoviesDB
{
    /// <summary>
    /// A simple class representing a UserRating
    /// </summary>
    public class UserRating
    {
        public UserRating()
        {
        }

        public string ImdbUserId { get; set; }
        public string ImdbMovieId { get; set; }
        public DateTime RatingDate { get; set; }
        public int Rating { get; set; }
    }   
}