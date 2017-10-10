using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FxMovies.FxMoviesDB;
using FxMovies.ImdbDB;
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

    [Flags]
    public enum Cert
    {
        all = 0,
        none = 1,
        other = 2,
        g = 4,
        pg = 8,
        pg13 = 16,
        r = 32,
        nc17 = 64,
        all2 = 127,
    }

    public class IndexModel : PageModel
    {
        public const decimal NO_IMDB_ID = -1.0m;
        public const decimal NO_IMDB_RATING = -2.0m;
        public const int FilterMaxDaysDefault = 8;
        public IList<Record> Records = new List<Record>();
        public string ImdbUserId = null;
        public DateTime? RefreshRequestTime = null;
        public DateTime? LastRefreshRatingsTime = null;
        public bool? LastRefreshSuccess = null;       

        public decimal? FilterMinRating = null;
        public bool? FilterNotYetRated = null;
        public Cert FilterCert = Cert.all;
        public int FilterMaxDays = FilterMaxDaysDefault;

        public int Count = 0;
        public int CountMinRating5 = 0;
        public int CountMinRating6 = 0;
        public int CountMinRating7 = 0;
        public int CountMinRating8 = 0;
        public int CountMinRating9 = 0;
        public int CountNotOnImdb = 0;
        public int CountNotRatedOnImdb = 0;
        public int CountNotYetRated = 0;
        public int CountRated = 0;
        public int CountCertNone = 0;
        public int CountCertG = 0;
        public int CountCertPG = 0;
        public int CountCertPG13 = 0;
        public int CountCertR = 0;
        public int CountCertNC17 = 0;
        public int CountCertOther = 0;
        public int Count3days = 0;
        public int Count5days = 0;
        public int Count8days = 0;
        public bool AdminMode = false;
        public string Password = null;
        public int AdsInterval = 5;

        private string Hash(string password)
        {
            var bytes = (new System.Text.UTF8Encoding()).GetBytes(password);
            byte[] hashBytes;
            using (var algorithm = new System.Security.Cryptography.SHA512Managed())
            {
                hashBytes = algorithm.ComputeHash(bytes);
            }
            return Convert.ToBase64String(hashBytes);
        }

        public void OnGet(decimal? minrating = null, bool? notyetrated = null, Cert cert = Cert.all,
            int? movieeventid = null, string setimdbid = null, string password = null, int? maxdays = null)
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            var now = DateTime.Now;

            // Get the connection string
            string connectionString = configuration.GetConnectionString("FxMoviesDb");
            string connectionStringImdb = configuration.GetConnectionString("ImdbDb");

            AdsInterval = configuration.GetValue("AdsInterval", AdsInterval);

            if (!string.IsNullOrEmpty(password))
            {
                string salt = configuration["AdminPasswordSalt"];
                string correctPassword = configuration["AdminPassword"];
                string timeComponent = now.ToShortDateString() + now.ToString("HH");
                string correctHash = Hash($"{salt}*{correctPassword}*{timeComponent}");

                if (password == correctPassword || password == correctHash)
                {
                    Password = correctHash;
                    AdminMode = true;
                }
            }
 
            if (minrating.HasValue)
                FilterMinRating = minrating.Value;
            
            if (maxdays.HasValue)
                FilterMaxDays = maxdays.Value;

            FilterNotYetRated = notyetrated;
            FilterCert = cert & Cert.all2;
            if (FilterCert == Cert.all2)
                FilterCert = Cert.all;

            ImdbUserId = Request.Cookies["ImdbUserId"];

            using (var db = FxMoviesDbContextFactory.Create(connectionString))
            {
                if (AdminMode && movieeventid.HasValue && !string.IsNullOrEmpty(setimdbid))
                {
                    var match = Regex.Match(setimdbid, @"(tt\d+)");
                    if (match.Success)
                    {
                        setimdbid = match.Groups[0].Value;
                        var movieEvent = db.MovieEvents.Find(movieeventid.Value);
                        if (movieEvent != null)
                        {
                            using (var dbImdb = ImdbDbContextFactory.Create(connectionStringImdb))
                            {
                                var imdbMovie = dbImdb.Movies.Find(setimdbid);
                                if (imdbMovie != null)
                                {
                                    movieEvent.ImdbRating = imdbMovie.Rating;
                                    movieEvent.ImdbVotes = imdbMovie.Votes;
                                }
                            }
                            movieEvent.ImdbId = setimdbid;
                            db.SaveChanges();
                        } 
                    }
                }

                if (ImdbUserId != null)
                {
                    var user = db.Users.Find(ImdbUserId);
                    if (user != null)
                    {
                        RefreshRequestTime = user.RefreshRequestTime;
                        LastRefreshRatingsTime = user.LastRefreshRatingsTime;
                        LastRefreshSuccess = user.LastRefreshSuccess;
                        user.Usages++;
                        user.LastUsageTime = DateTime.UtcNow;
                        db.SaveChanges();
                    }
                }

                Count = db.MovieEvents.Count();
                CountMinRating5 = db.MovieEvents.Where(m => m.ImdbRating >= 50).Count();
                CountMinRating6 = db.MovieEvents.Where(m => m.ImdbRating >= 60).Count();
                CountMinRating7 = db.MovieEvents.Where(m => m.ImdbRating >= 70).Count();
                CountMinRating8 = db.MovieEvents.Where(m => m.ImdbRating >= 80).Count();
                CountMinRating9 = db.MovieEvents.Where(m => m.ImdbRating >= 90).Count();
                CountNotOnImdb = db.MovieEvents.Where(m => m.ImdbId == null).Count();
                CountNotRatedOnImdb = db.MovieEvents.Where(m => m.ImdbRating == null).Count();
                CountCertNone =  db.MovieEvents.Where(m => string.IsNullOrEmpty(m.Certification)).Count();
                CountCertG =  db.MovieEvents.Where(m => m.Certification == "US:G").Count();
                CountCertPG =  db.MovieEvents.Where(m => m.Certification == "US:PG").Count();
                CountCertPG13 =  db.MovieEvents.Where(m => m.Certification == "US:PG-13").Count();
                CountCertR =  db.MovieEvents.Where(m => m.Certification == "US:R").Count();
                CountCertNC17 =  db.MovieEvents.Where(m => m.Certification == "US:NC-17").Count();
                CountCertOther =  Count - CountCertNone - CountCertG - CountCertPG - CountCertPG13 - CountCertR - CountCertNC17;
                CountRated = db.MovieEvents.Where(
                    me => db.UserRatings.Where(ur => ur.ImdbUserId == ImdbUserId).Any(ur => ur.ImdbMovieId == me.ImdbId)).Count();
                CountNotYetRated = Count - CountRated;
                Count3days =  db.MovieEvents.Where(m => m.StartTime.Date <= now.Date.AddDays(3)).Count();
                Count5days =  db.MovieEvents.Where(m => m.StartTime.Date <= now.Date.AddDays(5)).Count();
                Count8days =  db.MovieEvents.Where(m => m.StartTime.Date <= now.Date.AddDays(8)).Count();

                Records = (
                    from me in db.MovieEvents.Include(m => m.Channel)
                    join ur in db.UserRatings.Where(ur => ur.ImdbUserId == ImdbUserId) on me.ImdbId equals ur.ImdbMovieId into urGroup
                    from ur in urGroup.DefaultIfEmpty(null)
                    where 
                        (FilterMaxDays == 0 || me.StartTime.Date <= now.Date.AddDays(FilterMaxDays))
                        &&
                        (me.EndTime >= now && me.StartTime >= now.AddMinutes(-15))
                        &&
                        (!FilterMinRating.HasValue 
                            || (FilterMinRating.Value == NO_IMDB_ID && me.ImdbId == null)
                            || (FilterMinRating.Value == NO_IMDB_RATING && me.ImdbRating == null)
                            || (FilterMinRating.Value >= 0.0m && (me.ImdbRating >= FilterMinRating.Value * 10)))
                        &&
                        (!FilterNotYetRated.HasValue || FilterNotYetRated.Value == (ur == null))
                        && 
                        (FilterCert == Cert.all || (ParseCertification(me.Certification) & FilterCert) != 0)
                    select new Record() { MovieEvent = me, UserRating = ur }
                ).ToList();

                // MovieEvents = db.MovieEvents.Include(m => m.Channel)
                //     .Where(m => !MinRating.HasValue || m.ImdbRating >= MinRating.Value * 10)
                //     .ToList();
            }
        }

        private static Cert ParseCertification(string certification)
        {
            switch (certification)
            {
                case null:
                case "":
                    return Cert.none;
                case "US:G":
                    return Cert.g;
                case "US:PG":
                    return Cert.pg;
                case "US:PG-13":
                    return Cert.pg13;
                case "US:R":
                    return Cert.r;
                case "US:NC-17":
                    return Cert.nc17;
                default:
                    return Cert.other;
            }
        }
    }
}
