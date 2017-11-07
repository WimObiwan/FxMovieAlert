using System;

namespace FxMovies.FxMoviesDB
{
    /// <summary>
    /// A simple class representing a MovieEvent
    /// </summary>
    public class MovieEvent
    {
        public MovieEvent()
        {
        }

        public int Id { get; set; }
        public string Title { get; set; }
        public int Year { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public Channel Channel { get; set; }
        public string PosterS { get; set; }
        public string PosterM { get; set; }
        [Obsolete]
        public string PosterL { get; set; } // Not used, to be removed when SQLite supports 'DropColumn' in migrations
        public int Duration { get; set; }
        public string Genre { get; set; }
        public string Content { get; set; }
        public string ImdbId { get; set; }
        public int? ImdbRating { get; set; }
        public int? ImdbVotes { get; set; }
        public string YeloUrl { get; set; }
        public string Certification { get; set; }
        public string PosterS_Local { get; set; }
        public string PosterM_Local { get; set; }
    }   
}