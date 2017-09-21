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
        public bool? CookieStored = false;        
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

            if (setimdbuserid == null)
            {
                ImdbUserId = Request.Cookies["ImdbUserId"];
            }
            else if (setimdbuserid == "remove")
            {
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
                    CookieStored = true;
                }
                else
                {
                    CookieStored = false;
                }
            }

            string connectionString = configuration.GetConnectionString("FxMoviesDb");

            using (var db = FxMoviesDbContextFactory.Create(connectionString))
            {
                if (ImdbUserId != null)
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
}
