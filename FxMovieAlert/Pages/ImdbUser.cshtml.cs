using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FileHelpers;
using FxMovies.Core;
using FxMovies.FxMoviesDB;
using FxMovies.ImdbDB;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FxMovieAlert.Pages
{
    public class ImdbUserModel : PageModel
    {
        private readonly IMovieCreationHelper movieCreationHelper;
        private readonly FxMoviesDbContext fxMoviesDbContext;
        private readonly ImdbDbContext imdbDbContext;
        public string WarningMessage = null;        
        public string ErrorMessage = null;        
        public string ImdbUserId = null;
        public DateTime? RefreshRequestTime = null;
        public DateTime? LastRefreshRatingsTime = null;
        public string LastRefreshRatingsResult = null;
        public bool? LastRefreshSuccess = null;       
        public DateTime? WatchListLastRefreshTime = null;
        public string WatchListLastRefreshRatingsResult = null;
        public bool? WatchListLastRefreshSuccess = null;
        public int UserRatingCount = 0;
        public int UserWatchListCount = 0;
        public DateTime? RatingLastDate = null;
        public string RatingLastMovie = null;
        public int? RatingLastRating = null;
        public DateTime? WatchListLastDate = null;
        public string WatchListLastMovie = null;
        
        public readonly List<Tuple<string, string, string>> LastImportErrors = new List<Tuple<string, string, string>>();

        private IConfiguration configuration;

        public ImdbUserModel(
            IConfiguration configuration, 
            IMovieCreationHelper movieCreationHelper, 
            FxMoviesDbContext fxMoviesDbContext,
            ImdbDbContext imdbDbContext)
        {
            this.configuration = configuration;
            this.movieCreationHelper = movieCreationHelper;
            this.fxMoviesDbContext = fxMoviesDbContext;
            this.imdbDbContext = imdbDbContext;
        }

        public void OnGet(bool forcerefresh = false, string setimdbuserid = null)
        {
            string userId = ClaimChecker.UserId(User.Identity);

            if (userId == null)
            {
                return;
            }

            if (setimdbuserid == null)
            {
                var user = fxMoviesDbContext.Users.Where(u => u.UserId == userId).SingleOrDefault();
                if (user != null)
                {
                    ImdbUserId = user.ImdbUserId;
                }
            }
            else if (setimdbuserid == "remove")
            {
                User user = fxMoviesDbContext.Users.SingleOrDefault(u => u.UserId == userId);
                fxMoviesDbContext.UserRatings.RemoveRange(user.UserRatings);
                fxMoviesDbContext.Users.Remove(user);
                fxMoviesDbContext.SaveChanges();

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
                    ImdbUserId = imdbuserid;

                    forcerefresh = true;
                }
                else
                {
                    ErrorMessage = string.Format("Er werd een ongeldige IMDb Gebruikers ID opgegeven: {0}.", setimdbuserid);
                }
            }

            if (ImdbUserId != null)
            {
                var user = fxMoviesDbContext.Users.Where(u => u.UserId == userId).SingleOrDefault();
                if (user == null)
                {
                    user = new User();
                    user.UserId = userId;
                    user.ImdbUserId = ImdbUserId;
                    fxMoviesDbContext.Users.Add(user);
                }

                if (forcerefresh)
                    user.RefreshRequestTime = DateTime.UtcNow;

                RefreshRequestTime = user.RefreshRequestTime;
                LastRefreshRatingsTime = user.LastRefreshRatingsTime;
                LastRefreshRatingsResult = user.LastRefreshRatingsResult;
                LastRefreshSuccess = user.LastRefreshSuccess;
                WatchListLastRefreshTime = user.WatchListLastRefreshTime;
                WatchListLastRefreshRatingsResult = user.WatchListLastRefreshResult;
                WatchListLastRefreshSuccess = user.WatchListLastRefreshSuccess;
                user.LastUsageTime = DateTime.UtcNow;
                fxMoviesDbContext.SaveChanges();

                UserRatingCount = fxMoviesDbContext.UserRatings.Count(ur => ur.User.UserId == userId);
                UserWatchListCount = fxMoviesDbContext.UserWatchLists.Where(ur => ur.UserId == userId).Count();
                var ratingLast = fxMoviesDbContext.UserRatings
                    .Include(ur => ur.Movie)
                    .Where(ur => ur.User.UserId == userId)
                    .OrderByDescending(ur => ur.RatingDate)
                    .FirstOrDefault();
                if (ratingLast != null)
                {
                    RatingLastDate = ratingLast.RatingDate;
                    RatingLastRating = ratingLast.Rating;
                    RatingLastMovie = ratingLast.Movie.ImdbId;
                    var movie = imdbDbContext.Movies.SingleOrDefault(m => m.ImdbId == RatingLastMovie);
                    if (movie != null)
                    {
                        RatingLastMovie = movie.PrimaryTitle;
                    }
                }
                var watchListLast = fxMoviesDbContext.UserWatchLists
                    .Where(uw => uw.UserId == userId)
                    .OrderByDescending(uw => uw.AddedDate)
                    .FirstOrDefault();
                if (watchListLast != null)
                {
                    WatchListLastDate = watchListLast.AddedDate;
                    WatchListLastMovie = watchListLast.ImdbMovieId;
                    var movie = imdbDbContext.Movies.Find(WatchListLastMovie);
                    if (movie != null)
                    {
                        WatchListLastMovie = movie.PrimaryTitle;
                    }
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

            bool ratings = Request.Form["type"].Contains("ratings");
            bool watchlist = Request.Form["type"].Contains("watchlist");

            if (ratings ^ watchlist == false) 
            {
                // Exactly 1 should be true
                return new BadRequestResult();
            }

            if (ratings)
            {
                OnPostRatings();
            }
            else if (watchlist)
            {
                OnPostWatchlist();
            }

            OnGet();

            return Page();
        }

        private void OnPostRatings()
        {
            string userId = ClaimChecker.UserId(User.Identity);

            int existingCount = 0;
            int newCount = 0;

            User user = fxMoviesDbContext.Users.Single(u => u.UserId == userId);
            foreach (var file in Request.Form.Files)
            {
                try
                {
                    var engine = new FileHelperAsyncEngine<ImdbUserRatingRecord>();
                    
                    var movieIdsInFile = new SortedSet<string>(); 
                    
                    using (var reader = new StreamReader(file.OpenReadStream()))
                    using (engine.BeginReadStream(reader))
                    foreach (var record in engine)
                    {
                        try
                        {
                            string _const = record.Const;
                            movieIdsInFile.Add(_const);

                            DateTime date = DateTime.ParseExact(record.DateAdded, 
                                new string[] {"yyyy-MM-dd", "ddd MMM d HH:mm:ss yyyy", "ddd MMM dd HH:mm:ss yyyy"},
                                CultureInfo.InvariantCulture.DateTimeFormat, DateTimeStyles.AllowWhiteSpaces);

                            // Ratings
                            // Const,Your Rating,Date Added,Title,URL,Title Type,IMDb Rating,Runtime (mins),Year,Genres,Num Votes,Release Date,Directors
                            int rating = int.Parse(record.YourRating);

                            var movie = movieCreationHelper.GetOrCreateMovieByImdbId(_const);
                            var userRating = fxMoviesDbContext.UserRatings.FirstOrDefault(ur => ur.User == user && ur.Movie == movie);
                            if (userRating == null)
                            {
                                userRating = new UserRating();
                                userRating.User = user;
                                userRating.Movie = movie;
                                fxMoviesDbContext.UserRatings.Add(userRating);
                                newCount++;
                            }
                            else
                            {
                                existingCount++;
                            }
                            userRating.Rating = rating;
                            userRating.RatingDate = date;
                        }
                        catch (Exception x)
                        {
                            LastImportErrors.Add(
                                Tuple.Create(
                                    $"Lijn {engine.LineNumber - 1} uit het ratings bestand '{file.FileName}' kon niet verwerkt worden.\n"
                                    + "De meest voorkomende reden is een aanpassing aan het bestandsformaat door IMDb.",
                                    x.ToString(),
                                    "danger"));
                        }
                    }

                    List<UserRating> itemsToRemove = 
                        fxMoviesDbContext.UserRatings
                            .Where(ur => !movieIdsInFile.Contains(ur.Movie.ImdbId))
                            .ToList();

                    fxMoviesDbContext.UserRatings.RemoveRange(itemsToRemove);

                    LastImportErrors.Add(
                        Tuple.Create(
                            $"Het ratings bestand '{file.FileName}' werd ingelezen. "
                            + $"{newCount} nieuwe en {existingCount} bestaande films.  {itemsToRemove.Count} films verwijderd.",
                            (string)null,
                            "success"));
                }
                catch (Exception x)
                {
                    LastImportErrors.Add(
                        Tuple.Create(
                            $"Het ratings bestand '{file.FileName}' kon niet ingelezen worden.\n"
                            + "De meest voorkomende reden is het omwisselen van Ratings en Watchlist bestanden, of een aanpassing aan "
                            + "het bestandsformaat door IMDb.",
                            x.ToString(),
                            "danger"));
                }

                fxMoviesDbContext.SaveChanges();
            }
        }

        private void OnPostWatchlist()
        {
            string userId = ClaimChecker.UserId(User.Identity);

            int existingCount = 0;
            int newCount = 0;

            LastImportErrors.Clear();

            foreach (var file in Request.Form.Files)
            {
                try
                {
                    var engine = new FileHelperAsyncEngine<ImdbUserWatchlistRecord>();

                    var movieIdsInFile = new SortedSet<string>(); 
                    
                    using (var reader = new StreamReader(file.OpenReadStream()))
                    using (engine.BeginReadStream(reader))
                    foreach (var record in engine)
                    {
                        try
                        {
                            string _const = record.Const;
                            movieIdsInFile.Add(_const);

                            DateTime date = DateTime.ParseExact(record.Created, 
                                new string[] {"yyyy-MM-dd", "ddd MMM d HH:mm:ss yyyy", "ddd MMM dd HH:mm:ss yyyy"},
                                CultureInfo.InvariantCulture.DateTimeFormat, DateTimeStyles.AllowWhiteSpaces);

                            // Watchlist
                            // Position,Const,Created,Modified,Description,Title,URL,Title Type,IMDb Rating,Runtime (mins),Year,Genres,Num Votes,Release Date,Directors
                            var userWatchList = fxMoviesDbContext.UserWatchLists.Find(userId, _const);
                            if (userWatchList == null)
                            {
                                userWatchList = new UserWatchListItem();
                                userWatchList.UserId = userId;
                                userWatchList.ImdbMovieId = _const;
                                fxMoviesDbContext.UserWatchLists.Add(userWatchList);
                                newCount++;
                            }
                            else
                            {
                                existingCount++;
                            }
                            userWatchList.AddedDate = date;
                        }
                        catch (Exception x)
                        {
                            LastImportErrors.Add(
                                Tuple.Create(
                                    $"Lijn {engine.LineNumber - 1} uit het watchlist bestand '{file.FileName}' kon niet verwerkt worden. "
                                    + "De meest voorkomende reden is een aanpassing aan het bestandsformaat door IMDb.",
                                    x.ToString(),
                                    "danger"));
                        }
                    }

                    List<UserWatchListItem> itemsToRemove = 
                        fxMoviesDbContext.UserWatchLists
                            .Where(ur => ur.UserId == userId && !movieIdsInFile.Contains(ur.ImdbMovieId))
                            .ToList();

                    fxMoviesDbContext.UserWatchLists.RemoveRange(itemsToRemove);
                    
                    LastImportErrors.Add(
                        Tuple.Create(
                            $"Het watchlist bestand '{file.FileName}' werd ingelezen. "
                            + $"{newCount} nieuwe en {existingCount} bestaande films.  {itemsToRemove.Count} films verwijderd.",
                            (string)null,
                            "success"));
                }
                catch (Exception x)
                {
                    LastImportErrors.Add(
                        Tuple.Create(
                            $"Het watchlist bestand '{file.FileName}' kon niet ingelezen worden.\n"
                            + "De meest voorkomende reden is het omwisselen van Ratings en Watchlist bestanden, of een aanpassing aan "
                            + "het bestandsformaat door IMDb.",
                            x.ToString(),
                            "danger"));
                }

                fxMoviesDbContext.SaveChanges();
            }
        }

    }

    #pragma warning disable CS0649
    [IgnoreFirst]
    [DelimitedRecord(",")]
    class ImdbUserRatingRecord
    {
        // Const,Your Rating,Date Added,Title,URL,Title Type,IMDb Rating,Runtime (mins),Year,Genres,Num Votes,Release Date,Directors
        [FieldQuoted]
        public string Const;
        [FieldQuoted]
        public string YourRating;
        [FieldQuoted]
        //[FieldConverter(ConverterKind.DateMultiFormat, "ddd MMM d HH:mm:ss yyyy", "ddd MMM  d HH:mm:ss yyyy")]
        // 'Wed Sep 20 00:00:00 2017'
        public string DateAdded;
        [FieldQuoted]
        public string Title;
        [FieldQuoted]
        public string Url;
        [FieldQuoted]
        public string TitleType;
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
        public string Directors;

    }

    [IgnoreFirst]
    [DelimitedRecord(",")]
    class ImdbUserWatchlistRecord
    {
        // Position,Const,Created,Modified,Description,Title,URL,Title Type,IMDb Rating,Runtime (mins),Year,Genres,Num Votes,Release Date,Directors

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
        public string Url;
        [FieldQuoted]
        public string TitleType;
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
        public string Directors;
        [FieldQuoted]
        public string YourRating;
        [FieldQuoted]
        public string Rated;

    }

}
