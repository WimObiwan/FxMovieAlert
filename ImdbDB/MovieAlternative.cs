using System;
using System.ComponentModel.DataAnnotations;

namespace FxMovies.ImdbDB
{
    public class MovieAlternative
    {
        [Key]
        public string Id { get; set; }
        public string AlternativeTitle { get; set; }
    }
}
