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

        public int Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string LogoS { get; set; }
        public string LogoS_Local { get; set; }
    }   
}