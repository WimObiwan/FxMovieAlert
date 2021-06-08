using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FxMovies.ImdbDB
{
    public class MovieAlternative
    {
        public int Id { get; set; }
        public Movie Movie { get; set; }
        public string AlternativeTitle { get; set; }
        public string Normalized { get; set; }
        public int MovieId { get; set; }
    }
}
