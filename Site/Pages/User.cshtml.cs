using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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
    private readonly ImdbDbContext _imdbDbContext;
    private readonly IImdbRatingsFromFileService _imdbRatingsFromFileService;
    private readonly IImdbWatchlistFromFileService _imdbWatchlistFromFileService;
    private readonly MoviesDbContext _moviesDbContext;
    private readonly IUserRatingsRepository _userRatingsRepository;
    private readonly IUserWatchlistRepository _userWatchlistRepository;

    public readonly List<Tuple<string, string, string>> LastImportErrors = new();
    public readonly string WarningMessage;
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
    public DateTime? WatchListLastDate;
    public string WatchListLastMovie;
    public string WatchListLastRefreshRatingsResult;
    public bool? WatchListLastRefreshSuccess;
    public DateTime? WatchListLastRefreshTime;

    public UserModel(
        IUserRatingsRepository userRatingsRepository,
        IUserWatchlistRepository userWatchlistRepository,
        IImdbRatingsFromFileService imdbRatingsFromFileService,
        IImdbWatchlistFromFileService imdbWatchlistFromFileService,
        MoviesDbContext moviesDbContext,
        ImdbDbContext imdbDbContext)
    {
        _userRatingsRepository = userRatingsRepository;
        _userWatchlistRepository = userWatchlistRepository;
        _imdbRatingsFromFileService = imdbRatingsFromFileService;
        _imdbWatchlistFromFileService = imdbWatchlistFromFileService;
        _moviesDbContext = moviesDbContext;
        _imdbDbContext = imdbDbContext;
    }

    public async Task OnGet(string setimdbuserid = null)
    {
        var userId = ClaimChecker.UserId(User.Identity);
        bool forcerefresh = false;

        if (userId == null) return;

        if (setimdbuserid == null)
        {
            var user = await _moviesDbContext.Users.Where(u => u.UserId == userId).SingleOrDefaultAsync();
            if (user != null) ImdbUserId = user.ImdbUserId;
        }
        else if (setimdbuserid == "remove")
        {
            var user = await _moviesDbContext.Users.SingleOrDefaultAsync(u => u.UserId == userId);
            if (user != null)
            {
                _moviesDbContext.UserRatings.RemoveRange(user.UserRatings);
                _moviesDbContext.Users.Remove(user);
                await _moviesDbContext.SaveChangesAsync();
            }

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
                ErrorMessage = string.Format($"Er werd een ongeldige IMDb Gebruikers ID opgegeven: {setimdbuserid}.");
            }
        }

        if (ImdbUserId != null)
        {
            var user = await _moviesDbContext.Users.Where(u => u.UserId == userId).SingleOrDefaultAsync();
            if (user == null)
            {
                user = new User
                {
                    UserId = userId,
                    ImdbUserId = ImdbUserId
                };
                _moviesDbContext.Users.Add(user);
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
            await _moviesDbContext.SaveChangesAsync();

            UserRatingCount = await _moviesDbContext.UserRatings.CountAsync(ur => ur.User.UserId == userId);
            UserWatchListCount =
                await _moviesDbContext.UserWatchLists.Where(ur => ur.User.UserId == userId).CountAsync();
            var ratingLast = await _moviesDbContext.UserRatings
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
                var movie = await _imdbDbContext.Movies.SingleOrDefaultAsync(m => m.ImdbId == RatingLastMovie);
                if (movie != null) RatingLastMovie = movie.PrimaryTitle;
            }

            var watchListLast = await _moviesDbContext.UserWatchLists
                .Include(uw => uw.Movie)
                .Where(uw => uw.User.UserId == userId)
                .OrderByDescending(uw => uw.AddedDate)
                .ThenByDescending(uw => uw.Id)
                .FirstOrDefaultAsync();
            if (watchListLast != null)
            {
                WatchListLastDate = watchListLast.AddedDate;
                WatchListLastMovie = watchListLast.Movie.ImdbId;
                var movie = await _imdbDbContext.Movies.SingleOrDefaultAsync(m => m.ImdbId == WatchListLastMovie);
                if (movie != null) WatchListLastMovie = movie.PrimaryTitle;
            }
        }
    }

    public async Task<IActionResult> OnPostForceRefresh()
    {
        var userId = ClaimChecker.UserId(User.Identity);

        if (userId == null)
            return RedirectToPage();

        var user = await _moviesDbContext.Users.Where(u => u.UserId == userId).SingleOrDefaultAsync();
        if (user != null)
        {
            user.RefreshRequestTime = DateTime.UtcNow;
            await _moviesDbContext.SaveChangesAsync();
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPost()
    {
        if (Request.Form.Files.Count == 0)
            // Missing file
            return new BadRequestResult();

        var type = Request.Form["type"];
        var ratings = type.Contains("ratings");
        var watchlist = type.Contains("watchlist");

        if ((ratings ^ watchlist) == false)
            // Exactly 1 should be true
            return new BadRequestResult();

        if (Request.Form.Files.Count != 1)
            // Exactly 1 file should be provided
            return new BadRequestResult();

        var file = Request.Form.Files.Single();

        if (ratings)
            await OnPostRatings(file);
        else //if (watchlist)
            await OnPostWatchlist(file);

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
            await using var stream = file.OpenReadStream();
            imdbRatings = _imdbRatingsFromFileService.GetRatings(stream, out var lastImportErrors);
            if (lastImportErrors != null)
                LastImportErrors.AddRange(lastImportErrors);
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

        var result = await _userRatingsRepository.StoreByUserId(userId, imdbRatings, true);

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
            await using var stream = file.OpenReadStream();
            imdbWatchlist = _imdbWatchlistFromFileService.GetWatchlist(stream, out var lastImportErrors);
            LastImportErrors.Clear();
            if (lastImportErrors != null)
                LastImportErrors.AddRange(lastImportErrors);
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

        var result = await _userWatchlistRepository.StoreByUserId(userId, imdbWatchlist, true);

        LastImportErrors.Add(
            Tuple.Create(
                $"Het watchlist bestand '{file.FileName}' werd ingelezen. "
                + $"{result.NewCount} nieuwe en {result.ExistingCount} bestaande films.  {result.RemovedCount} films verwijderd.",
                (string)null,
                "success"));
    }
}