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
    public class Record
    {
        public MovieEvent MovieEvent { get; set; }
        public UserRating UserRating { get; set; }
    }

    public class IndexModel : PageModel
    {
        public IList<Record> Records = new List<Record>();
        public decimal? MinRating = null;
        public bool NotYetRated = false;

        public void OnGet(decimal? minrating = null, bool notyetrated = false)
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            // Get the connection string
            string connectionString = configuration.GetConnectionString("FxMoviesDb");

            if (minrating.HasValue)
                MinRating = minrating.Value;

            NotYetRated = notyetrated;

            using (var db = FxMoviesDbContextFactory.Create(connectionString))
            {
                Records = (
                    from me in db.MovieEvents.Include(m => m.Channel)
                    join ur in db.UserRatings on me.ImdbId equals ur.ImdbMovieId into urGroup
                    from ur in urGroup.DefaultIfEmpty(null)
                    where 
                        (!MinRating.HasValue || me.ImdbRating >= MinRating.Value * 10)
                        &&
                        (!NotYetRated || ur == null)
                    select new Record() { MovieEvent = me, UserRating = ur }
                ).ToList();
                // MovieEvents = db.MovieEvents.Include(m => m.Channel)
                //     .Where(m => !MinRating.HasValue || m.ImdbRating >= MinRating.Value * 10)
                //     .ToList();
            }
        }
    }
}
