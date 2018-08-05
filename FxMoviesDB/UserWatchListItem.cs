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

        public string UserId { get; set; }
        public string ImdbMovieId { get; set; }
        public DateTime AddedDate { get; set; }
    }
}