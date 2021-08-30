using System.Collections.Generic;

namespace FxMovies.Core.Entities

{
    public class ImdbMovie
    {
        public int Id { get; set; }
        public string PrimaryTitle { get; set; }
        //public string OriginalTitle { get; set; }
        public string ImdbId { get; set; }
        public int? Year {get; set; }
        public int? Votes { get; set; }
        public int? Rating { get; set; } // 100

        public List<ImdbMovieAlternative> MovieAlternatives { get; set; }
    }
}
