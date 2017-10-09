using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FileHelpers;
using FxMovies.FxMoviesDB;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
        public int UserRatingCount = 0;

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
                    
                    int expirationDays = configuration.GetValue("LoginCookieExpirationDays", 30);
                    CookieOptions options = new CookieOptions();
                    options.Expires = DateTime.Now.AddDays(expirationDays);
                    Response.Cookies.Append("ImdbUserId", imdbuserid, options);
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
            if (ImdbUserId != null)
            {
                var user = db.Users.Find(ImdbUserId);
                if (user != null)
                {
                    if (forcerefresh)
                        user.RefreshRequestTime = DateTime.UtcNow;

                    RefreshRequestTime = user.RefreshRequestTime;
                    LastRefreshRatingsTime = user.LastRefreshRatingsTime;
                    LastRefreshRatingsResult = user.LastRefreshRatingsResult;
                    LastRefreshSuccess = user.LastRefreshSuccess;
                    user.LastUsageTime = DateTime.UtcNow;
                    db.SaveChanges();

                    UserRatingCount = db.UserRatings.Where(ur => ur.ImdbUserId == ImdbUserId).Count();
                }
            }
        }

        public IActionResult OnPost()
        {
            if (Request.Form.Files.Count == 0)
            {
                // Missing file
                return new BadRequestResult();
            }

            string imdbUserId = Request.Cookies["ImdbUserId"];

            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            int existingCount = 0;
            int newCount = 0;

            string connectionString = configuration.GetConnectionString("FxMoviesDb");
            using (var db = FxMoviesDbContextFactory.Create(connectionString))
            foreach (var file in Request.Form.Files)
            {
                try
                {
                    var engine = new FileHelperAsyncEngine<ImdbUserRatingRecord>();
                    
                    using (var reader = new StreamReader(file.OpenReadStream()))
                    using (engine.BeginReadStream(reader))
                    foreach (var record in engine)
                    {
                        try
                        {
                            string _const = record.Const;
                            int rating = int.Parse(record.YouRated);
                            DateTime ratingDate = DateTime.ParseExact(record.Created, 
                                new string[] {"ddd MMM d HH:mm:ss yyyy", "ddd MMM dd HH:mm:ss yyyy"},
                                CultureInfo.InvariantCulture.DateTimeFormat, DateTimeStyles.AllowWhiteSpaces);

                            var userRating = db.UserRatings.Find(imdbUserId, _const);
                            if (userRating == null)
                            {
                                userRating = new UserRating();
                                userRating.ImdbUserId = imdbUserId;
                                userRating.ImdbMovieId = _const;
                                db.UserRatings.Add(userRating);
                                newCount++;
                            }
                            else
                            {
                                existingCount++;
                            }
                            userRating.Rating = rating;
                            userRating.RatingDate = ratingDate;
                        }
                        catch (Exception x)
                        {
                        }
                    }
                }
                catch (Exception x)
                {
                    Console.WriteLine($"Failed to read file, Error: {x.ToString()}");
                }

                db.SaveChanges();
            }

            OnGet();

            return Page();
        }
    }

    [IgnoreFirst]
    [DelimitedRecord(",")]
    class ImdbUserRatingRecord
    {
        [FieldQuoted]
        public string Position;
        [FieldQuoted]
        public string Const;
        [FieldQuoted]
        //[FieldConverter(ConverterKind.DateMultiFormat, "ddd MMM d HH:mm:ss yyyy", "ddd MMM  d HH:mm:ss yyyy")]
        // 'Wed Sep 20 00:00:00 2017'
        public string Created;
        //[FieldQuoted]
        //[FieldConverter(ConverterKind.DateMultiFormat, "ddd MMM d HH:mm:ss yyyy", "ddd MMM  d HH:mm:ss yyyy")]
        // 'Wed Sep 20 00:00:00 2017'?
        public string Modified;
        [FieldQuoted]
        public string description;
        [FieldQuoted]
        public string Title;
        [FieldQuoted]
        public string TitleType;
        [FieldQuoted]
        public string Directors;
        [FieldQuoted]
        public string YouRated;
        [FieldQuoted]
        public string IMDbRating;
        [FieldQuoted]
        public string Runtime;
        [FieldQuoted]
        public string Year;
        [FieldQuoted]
        public string Genres;
        [FieldQuoted]
        public string NumVotes;
        [FieldQuoted]
        //[FieldConverter(ConverterKind.Date, "yyyy-MM-dd")]
        public string ReleaseDate;
        [FieldQuoted]
        public string URL;

    }
}
