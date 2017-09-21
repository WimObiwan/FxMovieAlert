using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FxMovies.FxMoviesDB;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;

namespace FxMovieAlert.Pages
{
    public class ImdbUserModel : PageModel
    {
        public string ImdbUserId = null;
        public DateTime? RefreshRequestTime = null;
        public DateTime? LastRefreshRatingsTime = null;
        public string LastRefreshRatingsResult = null;       
        public bool? LastRefreshSuccess = null;       

        public void OnGet()
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            ImdbUserId = configuration.GetSection("Temp")["ImdbUserId"];
            string connectionString = configuration.GetConnectionString("FxMoviesDb");

            using (var db = FxMoviesDbContextFactory.Create(connectionString))
            {
                var user = db.Users.Find(ImdbUserId);
                if (user != null)
                {
                    RefreshRequestTime = user.RefreshRequestTime;
                    LastRefreshRatingsTime = user.LastRefreshRatingsTime;
                    LastRefreshRatingsResult = user.LastRefreshRatingsResult;
                    LastRefreshSuccess = user.LastRefreshSuccess;
                    user.LastUsageTime = DateTime.Now;
                    db.SaveChanges();
                }
            }
        }
    }
}
