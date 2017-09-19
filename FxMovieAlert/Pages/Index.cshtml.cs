using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FxMovies.FxMoviesDB;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FxMovieAlert.Pages
{
    public class IndexModel : PageModel
    {
        public IList<MovieEvent> MovieEvents = new List<MovieEvent>();
        public decimal? MinRating = null;

        public void OnGet(decimal? minrating = null)
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            // Get the connection string
            string connectionString = configuration.GetConnectionString("FxMoviesDb");

            if (minrating.HasValue)
                MinRating = minrating.Value;

            using (var db = FxMoviesDbContextFactory.Create(connectionString))
            {
                MovieEvents = db.MovieEvents.Include(m => m.Channel)
                    .Where(m => !MinRating.HasValue || m.ImdbRating >= MinRating.Value * 10)
                    .ToList();
            }
        }
    }
}
