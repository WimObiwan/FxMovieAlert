using System.ComponentModel.DataAnnotations;

namespace FxMovies.FxMoviesDB
{
    /// <summary>
    /// A simple class representing a Channel
    /// </summary>
    public class Channel
    {
        public Channel()
        {
        }

        [Key]
        public string Code { get; set; }
        public string Name { get; set; }
        public string LogoS { get; set; }
        public string LogoM { get; set; }
        public string LogoL { get; set; }
    }   
}