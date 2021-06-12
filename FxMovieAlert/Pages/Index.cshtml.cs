using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FxMovies.Core;
using FxMovies.FxMoviesDB;
using FxMovies.ImdbDB;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
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
        public UserWatchListItem UserWatchListItem { get; set; }
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

    [AllowAnonymous]
    public class ZendersModel : PageModel
    {
        public const decimal NO_IMDB_ID = -1.0m;
        public const decimal NO_IMDB_RATING = -2.0m;
        const string ClaimEditImdbLinks = "edit:imdblinks";
        public bool EditImdbLinks = false;
        public MovieEvent MovieEvent = null;
        public IList<Record> Records = new List<Record>();
        public string ImdbUserId = null;
        public DateTime? RefreshRequestTime = null;
        public DateTime? LastRefreshRatingsTime = null;
        public bool? LastRefreshSuccess = null;       

        public int FilterTypeMask = 1;
        public int FilterTypeMaskDefault = 1;
        public decimal? FilterMinRating = null;
        public bool? FilterNotYetRated = null;
        public Cert FilterCert = Cert.all;
        public int FilterMaxDaysDefault = 8;
        public int FilterMaxDays = 8;

        public int Count = 0;
        public int CountTypeFilm = 0;
        public int CountTypeShort = 0;
        public int CountTypeSerie = 0;
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
        public int AdsInterval = 5;

        private readonly IConfiguration configuration;
        private readonly FxMoviesDbContext fxMoviesDbContext;
        private readonly IMovieCreationHelper movieCreationHelper;

        public ZendersModel(IConfiguration configuration, FxMoviesDbContext fxMoviesDbContext,
            IMovieCreationHelper movieCreationHelper)
        {
            this.configuration = configuration;
            this.fxMoviesDbContext = fxMoviesDbContext;
            this.movieCreationHelper = movieCreationHelper;
        }

        public void OnGet(int? m = null, int? typeMask = null, decimal? minrating = null, bool? notyetrated = null, Cert cert = Cert.all,
            int? movieeventid = null, string setimdbid = null, int? maxdays = null)
        {
            var dbMovieEvents = fxMoviesDbContext.MovieEvents.Where(me => !me.Vod);
            string userId = ClaimChecker.UserId(User.Identity);

            var now = DateTime.Now;

            // Get the connection string
            string connectionString = configuration.GetConnectionString("FxMoviesDb");
            string connectionStringImdb = configuration.GetConnectionString("ImdbDb");

            AdsInterval = configuration.GetValue("AdsInterval", AdsInterval);
            FilterMaxDaysDefault = configuration.GetValue("DefaultMaxDays", FilterMaxDaysDefault);
            FilterMaxDays = FilterMaxDaysDefault;
            FilterTypeMask = FilterTypeMaskDefault;

            EditImdbLinks = ClaimChecker.Has(User.Identity, ClaimEditImdbLinks);

            if (typeMask.HasValue)
                FilterTypeMask = typeMask.Value;

            if (minrating.HasValue)
                FilterMinRating = minrating.Value;
            
            // Only allow setting more days when authenticated
            if (maxdays.HasValue && User.Identity.IsAuthenticated)
                FilterMaxDays = maxdays.Value;

            FilterNotYetRated = notyetrated;
            FilterCert = cert & Cert.all2;
            if (FilterCert == Cert.all2)
                FilterCert = Cert.all;

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
                    var movieEvent = dbMovieEvents.SingleOrDefault(me => me.Id == movieeventid.Value);
                    if (movieEvent != null)
                    {
                        if (movieEvent.Movie != null && movieEvent.Movie.ImdbId == setimdbid)
                        {
                            // Already ok, Do nothing
                        }
                        else
                        {
                            movieEvent.Movie = null;
                            if (setimdbid != null)
                            {
                                FxMovies.FxMoviesDB.Movie movie = movieCreationHelper.GetOrCreateMovieByImdbId(setimdbid);
                                movieEvent.Movie = movie;
                                movie.ImdbId = setimdbid; 

                                using (var dbImdb = ImdbDbContextFactory.Create(connectionStringImdb))
                                {
                                    var imdbMovie = dbImdb.Movies.SingleOrDefault(m => m.ImdbId == setimdbid);
                                    if (imdbMovie != null)
                                    {
                                        movieEvent.Movie.ImdbRating = imdbMovie.Rating;
                                        movieEvent.Movie.ImdbVotes = imdbMovie.Votes;
                                        if (!movieEvent.Year.HasValue)
                                            movieEvent.Year = imdbMovie.Year;
                                    }
                                }
                            }
                        }

                        fxMoviesDbContext.SaveChanges();
                    } 
                }
            }

            if (userId != null)
            {
                var user = fxMoviesDbContext.Users.Where(u => u.UserId == userId).SingleOrDefault();
                if (user != null)
                {
                    RefreshRequestTime = user.RefreshRequestTime;
                    LastRefreshRatingsTime = user.LastRefreshRatingsTime;
                    LastRefreshSuccess = user.LastRefreshSuccess;
                    user.Usages++;
                    user.LastUsageTime = DateTime.UtcNow;
                    ImdbUserId = user.ImdbUserId;
                    fxMoviesDbContext.SaveChanges();
                }
            }

            if (m.HasValue)
            {
                if (m.Value == -2)
                {
                    throw new Exception("Sentry test exception");
                }
                else
                {
                    MovieEvent = dbMovieEvents.SingleOrDefault(me => me.Id == m.Value);
                    if (MovieEvent != null)
                    {
                        int days = (int)(MovieEvent.StartTime.Date - DateTime.Now.Date).TotalDays;
                        if (FilterMaxDays < days)
                            FilterMaxDays = days;
                    }
                }
            }

            Count = dbMovieEvents.Count();
            CountTypeFilm = dbMovieEvents.Where(me => me.Type == 1).Count();
            CountTypeShort = dbMovieEvents.Where(me => me.Type == 2).Count();
            CountTypeSerie = dbMovieEvents.Where(me => me.Type == 3).Count();
            CountMinRating5 = dbMovieEvents.Where(me => me.Movie.ImdbRating >= 50).Count();
            CountMinRating6 = dbMovieEvents.Where(me => me.Movie.ImdbRating >= 60).Count();
            CountMinRating7 = dbMovieEvents.Where(me => me.Movie.ImdbRating >= 70).Count();
            CountMinRating8 = dbMovieEvents.Where(me => me.Movie.ImdbRating >= 80).Count();
            CountMinRating9 = dbMovieEvents.Where(me => me.Movie.ImdbRating >= 90).Count();
            CountNotOnImdb = dbMovieEvents.Where(me => string.IsNullOrEmpty(me.Movie.ImdbId)).Count();
            CountNotRatedOnImdb = dbMovieEvents.Where(me => me.Movie.ImdbRating == null).Count();
            CountCertNone =  dbMovieEvents.Where(me => string.IsNullOrEmpty(me.Movie.Certification)).Count();
            CountCertG =  dbMovieEvents.Where(me => me.Movie.Certification == "US:G").Count();
            CountCertPG =  dbMovieEvents.Where(me => me.Movie.Certification == "US:PG").Count();
            CountCertPG13 =  dbMovieEvents.Where(me => me.Movie.Certification == "US:PG-13").Count();
            CountCertR =  dbMovieEvents.Where(me => me.Movie.Certification == "US:R").Count();
            CountCertNC17 =  dbMovieEvents.Where(me => me.Movie.Certification == "US:NC-17").Count();
            CountCertOther =  Count - CountCertNone - CountCertG - CountCertPG - CountCertPG13 - CountCertR - CountCertNC17;
            CountRated = dbMovieEvents.Count(me => me.Movie.UserRatings.Any(ur => ur.User.UserId == userId));
            CountNotYetRated = Count - CountRated;
            Count3days =  dbMovieEvents.Where(me => me.StartTime.Date <= now.Date.AddDays(3)).Count();
            Count5days =  dbMovieEvents.Where(me => me.StartTime.Date <= now.Date.AddDays(5)).Count();
            Count8days =  dbMovieEvents.Where(me => me.StartTime.Date <= now.Date.AddDays(8)).Count();

            Records = dbMovieEvents
                .Where(me => 
                    !me.Vod
                    && 
                    (FilterMaxDays == 0 || me.StartTime.Date <= now.Date.AddDays(FilterMaxDays))
                    &&
                    (me.EndTime >= now && me.StartTime >= now.AddMinutes(-30))
                    &&
                    (
                        ((FilterTypeMask & 1) == 1 && me.Type == 1)
                        || ((FilterTypeMask & 2) == 2 && me.Type == 2)
                        || ((FilterTypeMask & 4) == 4 && me.Type == 3)
                    )
                    &&
                    (!FilterMinRating.HasValue 
                        || (FilterMinRating.Value == NO_IMDB_ID && string.IsNullOrEmpty(me.Movie.ImdbId))
                        || (FilterMinRating.Value == NO_IMDB_RATING && me.Movie.ImdbRating == null)
                        || (FilterMinRating.Value >= 0.0m && (me.Movie.ImdbRating >= FilterMinRating.Value * 10)))
                    // && 
                    // (FilterCert == Cert.all || (ParseCertification(me.Movie.Certification) & FilterCert) != 0)
                )
                .Include(me => me.Channel)
                .Include(me => me.Movie)
                .Select(me => new Record()
                    {
                        MovieEvent = me,
                        UserRating = me.Movie.UserRatings.FirstOrDefault(ur => ur.User.UserId == userId),
                        UserWatchListItem = me.Movie.UserWatchListItems.FirstOrDefault(ur => ur.User.UserId == userId)
                    }
                )
                .ToList();
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

        public async Task OnGetLogin(string returnUrl = "/")
        {
            await HttpContext.ChallengeAsync("Auth0", new AuthenticationProperties() { RedirectUri = returnUrl });
        }

    }
}
