using System;
using System.ComponentModel.DataAnnotations;

namespace FxMovies.FxMoviesDB
{
    /// <summary>
    /// A simple class representing a UserWatchListItem
    /// </summary>
    public class UserWatchListItem
    {
        public UserWatchListItem()
        {
        }

        public int Id { get; set; }
        public User User { get; set; }
        public Movie Movie { get; set; }
        public DateTime AddedDate { get; set; }

        public int? UserId { get; set; }
        public int? MovieId { get; set; }
    }
}