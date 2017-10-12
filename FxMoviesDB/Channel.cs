using System;
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
        [Obsolete]
        public string LogoM { get; set; } // Not used, to be removed when SQLite supports 'DropColumn' in migrations
        [Obsolete]
        public string LogoL { get; set; } // Not used, to be removed when SQLite supports 'DropColumn' in migrations
        public string LogoS_Local { get; set; }
    }   
}