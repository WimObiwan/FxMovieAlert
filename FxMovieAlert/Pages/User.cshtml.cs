using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FxMovies.Core;
using FxMovies.Core.Entities;
using FxMovies.Core.Repositories;
using FxMovies.Core.Services;
using FxMovies.ImdbDB;
using FxMovies.MoviesDB;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FxMovies.Site.Pages;

public class UserModel : PageModel
{
    private readonly FxMoviesDbContext fxMoviesDbContext;
    private readonly ImdbDbContext imdbDbContext;
    private readonly IImdbRatingsFromFileService imdbRatingsFromFileService;
    private readonly IImdbWatchlistFromFileService imdbWatchlistFromFileService;

    public readonly List<Tuple<string, string, string>> LastImportErrors = new();
    private readonly IMovieCreationHelper movieCreationHelper;
    private readonly IUserRatingsRepository userRatingsRepository;
    private readonly IUserWatchlistRepository userWatchlistRepository;
    public string ErrorMessage;
    public string ImdbUserId;
    public string LastRefreshRatingsResult;
    public DateTime? LastRefreshRatingsTime;
    public bool? LastRefreshSuccess;
    public DateTime? RatingLastDate;
    public string RatingLastMovie;
    public int? RatingLastRating;
    public DateTime? RefreshRequestTime;
    public int UserRatingCount;
    public int UserWatchListCount;
    public string WarningMessage = null;
    public DateTime? WatchListLastDate;
    public string WatchListLastMovie;
    public string WatchListLastRefreshRatingsResult;
    public bool? WatchListLastRefreshSuccess;
    public DateTime? WatchListLastRefreshTime;

    public UserModel(
        IMovieCreationHelper movieCreationHelper,
        IUserRatingsRepository userRatingsRepository,
        IUserWatchlistRepository userWatchlistRepository,
        IImdbRatingsFromFileService imdbRatingsFromFileService,
        IImdbWatchlistFromFileService imdbWatchlistFromFileService,
        FxMoviesDbContext fxMoviesDbContext,
        ImdbDbContext imdbDbContext)
    {
        this.movieCreationHelper = movieCreationHelper;
        this.userRatingsRepository = userRatingsRepository;
        this.userWatchlistRepository = userWatchlistRepository;
        this.imdbRatingsFromFileService = imdbRatingsFromFileService;
        this.imdbWatchlistFromFileService = imdbWatchlistFromFileService;
        this.fxMoviesDbContext = fxMoviesDbContext;
        this.imdbDbContext = imdbDbContext;
    }

    public async Task OnGet(bool forcerefresh = false, string setimdbuserid = null)
    {
        var userId = ClaimChecker.UserId(User.Identity);

        if (userId == null) return;

        if (setimdbuserid == null)
        {
            var user = await fxMoviesDbContext.Users.Where(u => u.UserId == userId).SingleOrDefaultAsync();
            if (user != null) ImdbUserId = user.ImdbUserId;
        }
        else if (setimdbuserid == "remove")
        {
            var user = await fxMoviesDbContext.Users.SingleOrDefaultAsync(u => u.UserId == userId);
            fxMoviesDbContext.UserRatings.RemoveRange(user.UserRatings);
            fxMoviesDbContext.Users.Remove(user);
            await fxMoviesDbContext.SaveChangesAsync();

            ImdbUserId = null;
        }
        else
        {
            var match = Regex.Match(setimdbuserid, @"(ur\d+)");
            if (match.Success)
            {
                ImdbUserId = match.Groups[1].Value;
                forcerefresh = true;
            }
            else
            {
                ErrorMessage = string.Format("Er werd een ongeldige IMDb Gebruikers ID opgegeven: {0}.", setimdbuserid);
            }
        }

        if (ImdbUserId != null)
        {
            var user = await fxMoviesDbContext.Users.Where(u => u.UserId == userId).SingleOrDefaultAsync();
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
            await fxMoviesDbContext.SaveChangesAsync();

            UserRatingCount = await fxMoviesDbContext.UserRatings.CountAsync(ur => ur.User.UserId == userId);
            UserWatchListCount =
                await fxMoviesDbContext.UserWatchLists.Where(ur => ur.User.UserId == userId).CountAsync();
            var ratingLast = await fxMoviesDbContext.UserRatings
                .Include(ur => ur.Movie)
                .Where(ur => ur.User.UserId == userId)
                .OrderByDescending(ur => ur.RatingDate)
                .ThenByDescending(ur => ur.Id)
                .FirstOrDefaultAsync();
            if (ratingLast != null)
            {
                RatingLastDate = ratingLast.RatingDate;
                RatingLastRating = ratingLast.Rating;
                RatingLastMovie = ratingLast.Movie.ImdbId;
                var movie = await imdbDbContext.Movies.SingleOrDefaultAsync(m => m.ImdbId == RatingLastMovie);
                if (movie != null) RatingLastMovie = movie.PrimaryTitle;
            }

            var watchListLast = await fxMoviesDbContext.UserWatchLists
                .Include(uw => uw.Movie)
                .Where(uw => uw.User.UserId == userId)
                .OrderByDescending(uw => uw.AddedDate)
                .ThenByDescending(uw => uw.Id)
                .FirstOrDefaultAsync();
            if (watchListLast != null)
            {
                WatchListLastDate = watchListLast.AddedDate;
                WatchListLastMovie = watchListLast.Movie.ImdbId;
                var movie = await imdbDbContext.Movies.SingleOrDefaultAsync(m => m.ImdbId == WatchListLastMovie);
                if (movie != null) WatchListLastMovie = movie.PrimaryTitle;
            }
        }
    }

    public async Task<IActionResult> OnPost()
    {
        if (Request.Form.Files.Count == 0)
            // Missing file
            return new BadRequestResult();

        var ratings = Request.Form["type"].Contains("ratings");
        var watchlist = Request.Form["type"].Contains("watchlist");

        if (ratings ^ (watchlist == false))
            // Exactly 1 should be true
            return new BadRequestResult();

        if (Request.Form.Files.Count != 1)
            // Exactly 1 file should be provided
            return new BadRequestResult();

        var file = Request.Form.Files.Single();

        if (ratings)
            await OnPostRatings(file);
        else if (watchlist) await OnPostWatchlist(file);

        await OnGet();

        return Page();
    }

    private async Task OnPostRatings(IFormFile file)
    {
        var userId = ClaimChecker.UserId(User.Identity);
        LastImportErrors.Clear();

        IEnumerable<ImdbRating> imdbRatings;
        try
        {
            using (var stream = file.OpenReadStream())
            {
                imdbRatings = imdbRatingsFromFileService.GetRatings(stream, out var lastImportErrors);
                if (lastImportErrors != null)
                    LastImportErrors.AddRange(lastImportErrors);
            }
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

            return;
        }

        var result = await userRatingsRepository.StoreByUserId(userId, imdbRatings, true);

        LastImportErrors.Add(
            Tuple.Create(
                $"Het ratings bestand '{file.FileName}' werd ingelezen. "
                + $"{result.NewCount} nieuwe en {result.ExistingCount} bestaande films.  {result.RemovedCount} films verwijderd.",
                (string)null,
                "success"));
    }

    private async Task OnPostWatchlist(IFormFile file)
    {
        var userId = ClaimChecker.UserId(User.Identity);
        LastImportErrors.Clear();

        IEnumerable<ImdbWatchlist> imdbWatchlist;
        try
        {
            using (var stream = file.OpenReadStream())
            {
                imdbWatchlist = imdbWatchlistFromFileService.GetWatchlist(stream, out var lastImportErrors);
                LastImportErrors.Clear();
                if (lastImportErrors != null)
                    LastImportErrors.AddRange(lastImportErrors);
            }
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

            return;
        }

        var result = await userWatchlistRepository.StoreByUserId(userId, imdbWatchlist, true);

        LastImportErrors.Add(
            Tuple.Create(
                $"Het watchlist bestand '{file.FileName}' werd ingelezen. "
                + $"{result.NewCount} nieuwe en {result.ExistingCount} bestaande films.  {result.RemovedCount} films verwijderd.",
                (string)null,
                "success"));
    }
}