using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FileHelpers;
using FxMovies.FxMoviesDB;
using FxMovies.ImdbDB;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FxMovieAlert.Pages
{
    public class RecordVodMovie
    {
        public VodMovie VodMovie { get; set; }
        public UserRating UserRating { get; set; }
        public UserWatchListItem UserWatchListItem { get; set; }
    }

    public class YeloPlayModel : PageModel
    {
        public const decimal NO_IMDB_ID = -1.0m;
        public const decimal NO_IMDB_RATING = -2.0m;
        const string ClaimEditImdbLinks = "edit:imdblinks";
        public bool EditImdbLinks = false;
        public VodMovie VodMovie = null;
        public IList<RecordVodMovie> Records = new List<RecordVodMovie>();
        public string ImdbUserId = null;
        public DateTime? RefreshRequestTime = null;
        public DateTime? LastRefreshRatingsTime = null;
        public bool? LastRefreshSuccess = null;       
        public decimal? FilterMinRating = null;
        public bool? FilterNotYetRated = null;
        public Cert FilterCert = Cert.all;
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
        public int AdsInterval = 5;

        public void OnGet(int? m = null, decimal? minrating = null, bool? notyetrated = null, Cert cert = Cert.all,
            int? movieeventid = null, string setimdbid = null, int? maxdays = null)
        {
            string userId = ClaimChecker.UserId(User.Identity);

            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            var now = DateTime.Now;

            // Get the connection string
            string connectionString = configuration.GetConnectionString("FxMoviesDb");
            string connectionStringImdb = configuration.GetConnectionString("ImdbDb");

            AdsInterval = configuration.GetValue("AdsInterval", AdsInterval);

            EditImdbLinks = ClaimChecker.Has(User.Identity, ClaimEditImdbLinks);

            if (minrating.HasValue)
                FilterMinRating = minrating.Value;
            
            FilterNotYetRated = notyetrated;
            FilterCert = cert & Cert.all2;
            if (FilterCert == Cert.all2)
                FilterCert = Cert.all;

            using (var db = FxMoviesDbContextFactory.Create(connectionString))
            {
                if (EditImdbLinks && movieeventid.HasValue && !string.IsNullOrEmpty(setimdbid))
                {
                    bool overwrite = false;
                    var match = Regex.Match(setimdbid, @"(tt\d+)");
                    if (match.Success)
                    {
                        setimdbid = match.Groups[0].Value;
                        overwrite = true;
                    }
                    else if (setimdbid.Equals("remove", StringComparison.InvariantCultureIgnoreCase))
                    {
                        setimdbid = null;
                        overwrite = true;
                    }
                    
                    if (overwrite)
                    {
                        var vodMovie = db.VodMovies.Find(movieeventid.Value);
                        if (vodMovie != null)
                        {
                            if (setimdbid != null)
                                using (var dbImdb = ImdbDbContextFactory.Create(connectionStringImdb))
                                {
                                    var imdbMovie = dbImdb.Movies.Find(setimdbid);
                                    if (imdbMovie != null)
                                    {
                                        vodMovie.ImdbRating = imdbMovie.Rating;
                                        vodMovie.ImdbVotes = imdbMovie.Votes;
                                    }
                                }

                            vodMovie.ImdbId = setimdbid;
                            db.SaveChanges();
                        } 
                    }
                }

                if (userId != null)
                {
                    var user = db.Users.Find(userId);
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

                if (m.HasValue)
                    VodMovie = db.VodMovies.Find(m.Value);

                Count = db.VodMovies.Count();
                CountMinRating5 = db.VodMovies.Where(me => me.ImdbRating >= 50).Count();
                CountMinRating6 = db.VodMovies.Where(me => me.ImdbRating >= 60).Count();
                CountMinRating7 = db.VodMovies.Where(me => me.ImdbRating >= 70).Count();
                CountMinRating8 = db.VodMovies.Where(me => me.ImdbRating >= 80).Count();
                CountMinRating9 = db.VodMovies.Where(me => me.ImdbRating >= 90).Count();
                CountNotOnImdb = db.VodMovies.Where(me => string.IsNullOrEmpty(me.ImdbId)).Count();
                CountNotRatedOnImdb = db.VodMovies.Where(me => me.ImdbRating == null).Count();
                CountCertNone =  db.VodMovies.Where(me => string.IsNullOrEmpty(me.Certification)).Count();
                CountCertG =  db.VodMovies.Where(me => me.Certification == "US:G").Count();
                CountCertPG =  db.VodMovies.Where(me => me.Certification == "US:PG").Count();
                CountCertPG13 =  db.VodMovies.Where(me => me.Certification == "US:PG-13").Count();
                CountCertR =  db.VodMovies.Where(me => me.Certification == "US:R").Count();
                CountCertNC17 =  db.VodMovies.Where(me => me.Certification == "US:NC-17").Count();
                CountCertOther =  Count - CountCertNone - CountCertG - CountCertPG - CountCertPG13 - CountCertR - CountCertNC17;
                CountRated = db.VodMovies.Where(
                    me => db.UserRatings.Where(ur => ur.UserId == userId).Any(ur => ur.ImdbMovieId == me.ImdbId)).Count();
                CountNotYetRated = Count - CountRated;

                Records = (
                    from me in db.VodMovies
                    join ur in db.UserRatings.Where(ur => ur.UserId == userId) on me.ImdbId equals ur.ImdbMovieId into urGroup
                    from ur in urGroup.DefaultIfEmpty(null)
                    join uw in db.UserWatchLists.Where(ur => ur.UserId == userId) on me.ImdbId equals uw.ImdbMovieId into uwGroup
                    from uw in uwGroup.DefaultIfEmpty(null)
                    where 
                        (!FilterMinRating.HasValue 
                            || (FilterMinRating.Value == NO_IMDB_ID && string.IsNullOrEmpty(me.ImdbId))
                            || (FilterMinRating.Value == NO_IMDB_RATING && me.ImdbRating == null)
                            || (FilterMinRating.Value >= 0.0m && (me.ImdbRating >= FilterMinRating.Value * 10)))
                        &&
                        (!FilterNotYetRated.HasValue || FilterNotYetRated.Value == (ur == null))
                        && 
                        (FilterCert == Cert.all || (ParseCertification(me.Certification) & FilterCert) != 0)
                    select new RecordVodMovie() { VodMovie = me, UserRating = ur, UserWatchListItem = uw }
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
