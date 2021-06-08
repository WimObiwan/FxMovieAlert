using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FxMovies.ImdbDB
{
    public class Movie
    {
        public int Id { get; set; }
        public string PrimaryTitle { get; set; }
        //public string OriginalTitle { get; set; }
        public string ImdbId { get; set; }
        public int? Year {get; set; }
        public int? Votes { get; set; }
        public int? Rating { get; set; } // 100

        public List<MovieAlternative> MovieAlternatives { get; set; }
    }
}
