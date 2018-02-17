using System;

namespace FxMovies.FxMoviesDB
{
    /// <summary>
    /// A simple class representing a MovieEvent
    /// </summary>
    public class VodMovie : IHasImdbLink
    {
        public VodMovie()
        {
        }

        public string Provider { get; set; } // e.g. yelo
        public int PrividerId { get; set; } 
        public string ProviderCategory { get; set; } // e.g. Drama
        public int ProviderMask { get; set; } // e.g. 3 = Play (1) | PlayMore (2)
        public string Title { get; set; }
        public int? Year { get; set; }
        public int ProviderId { get; set; }
        public string Image { get; set; }
        public string Image_Local { get; set; }
        public decimal Price { get; set; }
        public DateTime ValidFrom { get; set; }
        public DateTime ValidUntil { get; set; }
        public string ImdbId { get; set; }
        public int? ImdbRating { get; set; }
        public int? ImdbVotes { get; set; }
        public string Certification { get; set; }
    }   
}