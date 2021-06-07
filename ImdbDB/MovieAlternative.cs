using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FxMovies.ImdbDB
{
    public class MovieAlternative
    {
        public string Id { get; set; }
        public int No { get; set; }
        public string AlternativeTitle { get; set; }

        [ForeignKey("Id")]
        public ImdbMovie Movie { get; set; }
    }
}
