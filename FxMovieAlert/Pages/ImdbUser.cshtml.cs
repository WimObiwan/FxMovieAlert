using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FxMovies.FxMoviesDB;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;

namespace FxMovieAlert.Pages
{
    public class ImdbUserModel : PageModel
    {
        public string WarningMessage = null;        
        public string ErrorMessage = null;        
        public string ImdbUserId = null;
        public DateTime? RefreshRequestTime = null;
        public DateTime? LastRefreshRatingsTime = null;
        public string LastRefreshRatingsResult = null;       
        public bool? LastRefreshSuccess = null;       

        public void OnGet(bool forcerefresh = false, string setimdbuserid = null)
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            string connectionString = configuration.GetConnectionString("FxMoviesDb");

            if (setimdbuserid == null)
            {
                ImdbUserId = Request.Cookies["ImdbUserId"];
            }
            else if (setimdbuserid == "remove")
            {
                using (var db = FxMoviesDbContextFactory.Create(connectionString))
                {
                    if (ImdbUserId != null)
                    {
                        db.Users.Remove(db.Users.Find(ImdbUserId));
                        db.UserRatings.RemoveRange(db.UserRatings.Where(ur => ur.ImdbUserId == ImdbUserId));
                    }
                    db.SaveChanges();
                }

                Response.Cookies.Delete("ImdbUserId");
                ImdbUserId = null;
            }
            else
            {
                var match = Regex.Match(setimdbuserid, @"(ur\d+)");
                if (match.Success)
                {
                    var imdbuserid = match.Groups[1].Value;
                    Response.Cookies.Append("ImdbUserId", imdbuserid);
                    ImdbUserId = setimdbuserid;

                    forcerefresh = true;

                    WarningMessage = string.Format("Een cookie werd op je computer geplaatst om je IMDB Gebruikers ID {0} te onthouden.", imdbuserid);
                }
                else
                {
                    ErrorMessage = string.Format("Er werd een ongeldige IMDB Gebruikers ID opgegeven: {0}.", setimdbuserid);
                }
            }

            using (var db = FxMoviesDbContextFactory.Create(connectionString))
            {
                if (ImdbUserId != null)
                {
                    var user = db.Users.Find(ImdbUserId);
                    if (user != null)
                    {
                        if (forcerefresh)
                        {
                            user.RefreshRequestTime = DateTime.UtcNow;
                        }
                        RefreshRequestTime = user.RefreshRequestTime;
                        LastRefreshRatingsTime = user.LastRefreshRatingsTime;
                        LastRefreshRatingsResult = user.LastRefreshRatingsResult;
                        LastRefreshSuccess = user.LastRefreshSuccess;
                        user.LastUsageTime = DateTime.UtcNow;
                        db.SaveChanges();
                    }
                }
            }
        }
    }
}
