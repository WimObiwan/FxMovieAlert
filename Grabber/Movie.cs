using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FxMovies.Grabber
{
    public class Movie
    {
        public string Title { get; internal set; }
        public int Year { get; internal set; }
        public Channel Channel { get; internal set; }
        public DateTime StartTime { get; internal set; }
        public DateTime EndTime { get; internal set; }
    }
}
