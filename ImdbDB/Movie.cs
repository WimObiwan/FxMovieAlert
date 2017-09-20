using System;
using System.ComponentModel.DataAnnotations;

namespace FxMovies.ImdbDB
{
    public class Movie
    {
        [Key]
        public string Id { get; set; }
        public string PrimaryTitle { get; set; }
        //public string OriginalTitle { get; set; }
        public int? Year {get; set; }
        public int? Votes { get; set; }
        public int? Rating { get; set; } // 100
    }
}
