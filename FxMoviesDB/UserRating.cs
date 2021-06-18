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

        public int Id { get; set; }
        public User User { get; set; }
        public Movie Movie { get; set; }
        public DateTime RatingDate { get; set; }
        public int Rating { get; set; }

        public int? UserId { get; set; }
        public int? MovieId { get; set; }
    }
}