using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FxMovies.Core;
using FxMovies.Core.Entities;
using FxMovies.Core.Repositories;
using FxMovies.MoviesDB;
using FxMovies.Site.Components;
using FxMovies.Site.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FxMovies.Site.Pages;

public class Record
{
    public MovieEvent MovieEvent { get; set; }
    public UserRating UserRating { get; set; }
    public UserWatchListItem UserWatchListItem { get; set; }
    public bool Highlighted { get; set; }
}

public class BroadcastsModelBase : PageModel, IFilterBarParentModel
{
    private readonly MovieEvent.FeedType _feed;
    private readonly MoviesDbContext _moviesDbContext;
    private readonly SiteOptions _siteOptions;
    private readonly IUsersRepository _usersRepository;
    public int AdsInterval = 5;
    public bool EditImdbLinks;
    public bool? LastRefreshSuccess;
    public MovieEvent MovieEvent;
    public IList<Record> Records = new List<Record>();

    public BroadcastsModelBase(
        MovieEvent.FeedType feed,
        IOptions<SiteOptions> siteOptions,
        MoviesDbContext moviesDbContext,
        IUsersRepository usersRepository)
    {
        _feed = feed;
        _siteOptions = siteOptions.Value;
        _moviesDbContext = moviesDbContext;
        _usersRepository = usersRepository;
    }

    public int HighlightedFilterMonthsThreshold { get; } = 36;
    public int HighlightedFilterRatingThreshold { get; } = 8;
    public string ImdbUserId { get; private set; }
    public DateTime? RefreshRequestTime { get; private set; }
    public DateTime? LastRefreshRatingsTime { get; private set; }

    public bool? FilterOnlyHighlights { get; private set; }
    public bool FilterOnlyHighlightsDefault { get; } = true;
    public int FilterTypeMask { get; private set; } = 1;
    public int FilterTypeMaskDefault { get; } = 1;
    public decimal? FilterMinRating { get; private set; }
    public bool? FilterNotYetRated { get; private set; }
    public Cert FilterCert { get; private set; } = Cert.all;
    public int FilterMaxDaysDefault { get; private set; } = 8;
    public int FilterMaxDays { get; private set; } = 8;

    public int Count { get; private set; }
    public int CountTypeFilm { get; private set; }
    public int CountTypeShort { get; private set; }
    public int CountTypeSerie { get; private set; }
    public int CountMinRating5 { get; private set; }
    public int CountMinRating6 { get; private set; }
    public int CountMinRating65 { get; private set; }
    public int CountMinRating7 { get; private set; }
    public int CountMinRating8 { get; private set; }
    public int CountMinRating9 { get; private set; }
    public int CountNotOnImdb { get; private set; }
    public int CountNotRatedOnImdb { get; private set; }
    public int CountNotYetRated { get; private set; }
    public int CountRated { get; private set; }
    public int CountCertNone { get; private set; }
    public int CountCertG { get; private set; }
    public int CountCertPG { get; private set; }
    public int CountCertPG13 { get; private set; }
    public int CountCertR { get; private set; }
    public int CountCertNC17 { get; private set; }
    public int CountCertOther { get; private set; }
    public int Count3days { get; private set; }
    public int Count5days { get; private set; }
    public int Count8days { get; private set; }

    public async Task OnGet(int? m = null, bool? onlyHighlights = null, int? typeMask = null, decimal? minrating = null,
        bool? notyetrated = null, Cert cert = Cert.all, int? maxdays = null)
    {
        var userId = ClaimChecker.UserId(User.Identity);

        var now = DateTime.Now;

        AdsInterval = _siteOptions.AdsInterval;
        FilterMaxDaysDefault = _siteOptions.DefaultMaxDays;
        FilterMaxDays = FilterMaxDaysDefault;
        FilterTypeMask = FilterTypeMaskDefault;

        EditImdbLinks = ClaimChecker.Has(User.Identity, Claims.EditImdbLinks);

        FilterOnlyHighlights = onlyHighlights;

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

        bool? streaming;
        switch (_feed)
        {
            case MovieEvent.FeedType.Broadcast:
                streaming = false;
                break;
            case MovieEvent.FeedType.FreeVod:
                streaming = true;
                break;
            default:
                streaming = null;
                break;
        }

        var dbMovieEvents =
            _moviesDbContext.MovieEvents.Where(me => me.Feed == _feed || me.Feed == null && me.Vod == streaming);

        if (_feed == MovieEvent.FeedType.Broadcast)
            dbMovieEvents = dbMovieEvents.Where(me =>
                (FilterMaxDays == 0 || me.StartTime.Date <= now.Date.AddDays(FilterMaxDays)) && me.EndTime >= now &&
                me.StartTime >= now.AddMinutes(-30));
        else
            dbMovieEvents = dbMovieEvents.Where(me => me.EndTime == null || me.EndTime >= now);

        if (userId != null)
        {
            var result = await _usersRepository.UpdateUserLastUsedAndGetData(userId);
            if (result != null)
            {
                RefreshRequestTime = result.RefreshRequestTime;
                LastRefreshRatingsTime = result.LastRefreshRatingsTime;
                LastRefreshSuccess = result.LastRefreshSuccess;
                ImdbUserId = result.ImdbUserId;
            }
        }

        if (m.HasValue)
        {
            if (m.Value == -2) throw new Exception("Sentry test exception");

            MovieEvent = dbMovieEvents
                .Include(me => me.Movie)
                .Include(me => me.Channel)
                .SingleOrDefault(me => me.Id == m.Value);
            if (MovieEvent != null)
            {
                var days = (int)(MovieEvent.StartTime.Date - DateTime.Now.Date).TotalDays;
                if (FilterMaxDays != 0 && FilterMaxDays < days)
                    FilterMaxDays = days;
            }
        }

        Count = await dbMovieEvents.CountAsync();
        CountTypeFilm = await dbMovieEvents.Where(me => me.Type == 1).CountAsync();
        CountTypeShort = await dbMovieEvents.Where(me => me.Type == 2).CountAsync();
        CountTypeSerie = await dbMovieEvents.Where(me => me.Type == 3).CountAsync();
        CountMinRating5 = await dbMovieEvents.Where(me => me.Movie.ImdbRating >= 50).CountAsync();
        CountMinRating6 = await dbMovieEvents.Where(me => me.Movie.ImdbRating >= 60).CountAsync();
        CountMinRating65 = await dbMovieEvents.Where(me => me.Movie.ImdbRating >= 65).CountAsync();
        CountMinRating7 = await dbMovieEvents.Where(me => me.Movie.ImdbRating >= 70).CountAsync();
        CountMinRating8 = await dbMovieEvents.Where(me => me.Movie.ImdbRating >= 80).CountAsync();
        CountMinRating9 = await dbMovieEvents.Where(me => me.Movie.ImdbRating >= 90).CountAsync();
        CountNotOnImdb = await dbMovieEvents
            .Where(me => me.Movie == null || string.IsNullOrEmpty(me.Movie.ImdbId) && !me.Movie.ImdbIgnore)
            .CountAsync();
        CountNotRatedOnImdb = await dbMovieEvents.Where(me => me.Movie.ImdbRating == null).CountAsync();
        CountCertNone = await dbMovieEvents.Where(me => string.IsNullOrEmpty(me.Movie.Certification)).CountAsync();
        CountCertG = await dbMovieEvents.Where(me => me.Movie.Certification == "US:G").CountAsync();
        CountCertPG = await dbMovieEvents.Where(me => me.Movie.Certification == "US:PG").CountAsync();
        CountCertPG13 = await dbMovieEvents.Where(me => me.Movie.Certification == "US:PG-13").CountAsync();
        CountCertR = await dbMovieEvents.Where(me => me.Movie.Certification == "US:R").CountAsync();
        CountCertNC17 = await dbMovieEvents.Where(me => me.Movie.Certification == "US:NC-17").CountAsync();
        CountCertOther = Count - CountCertNone - CountCertG - CountCertPG - CountCertPG13 - CountCertR - CountCertNC17;
        CountRated = await dbMovieEvents.CountAsync(me => me.Movie.UserRatings.Any(ur => ur.User.UserId == userId));
        CountNotYetRated = Count - CountRated;
        Count3days = await dbMovieEvents.Where(me => me.StartTime.Date <= now.Date.AddDays(3)).CountAsync();
        Count5days = await dbMovieEvents.Where(me => me.StartTime.Date <= now.Date.AddDays(5)).CountAsync();
        Count8days = await dbMovieEvents.Where(me => me.StartTime.Date <= now.Date.AddDays(8)).CountAsync();

        var tmp = dbMovieEvents
            .Where(me =>
                    (
                        (FilterTypeMask & 1) == 1 && me.Type == 1
                        || (FilterTypeMask & 2) == 2 && me.Type == 2
                        || (FilterTypeMask & 4) == 4 && me.Type == 3
                    )
                    &&
                    (!FilterMinRating.HasValue
                     || FilterMinRating.Value == FilterBar.NO_IMDB_ID && (me.Movie == null ||
                                                                          string.IsNullOrEmpty(me.Movie.ImdbId) &&
                                                                          !me.Movie.ImdbIgnore)
                     || FilterMinRating.Value == FilterBar.NO_IMDB_RATING && me.Movie.ImdbRating == null
                     || FilterMinRating.Value >= 0.0m && me.Movie.ImdbRating >= FilterMinRating.Value * 10)
                // && 
                // (FilterCert == Cert.all || (ParseCertification(me.Movie.Certification) & FilterCert) != 0)
            )
            .AsNoTracking()
            .Include(me => me.Channel)
            .Include(me => me.Movie)
            .Select(me => new
            {
                MovieEvent = me,
                UserRating = me.Movie.UserRatings.FirstOrDefault(ur => ur.User.UserId == userId),
                UserWatchListItem = me.Movie.UserWatchListItems.FirstOrDefault(ur => ur.User.UserId == userId)
            })
            .Select(r => new Record
                {
                    MovieEvent = r.MovieEvent,
                    UserRating = r.UserRating,
                    UserWatchListItem = r.UserWatchListItem,
                    Highlighted =
                        r.UserWatchListItem != null
                        ||
                        r.UserRating != null
                        && r.UserRating.Rating >= HighlightedFilterRatingThreshold
                        && r.UserRating.RatingDate < now.AddMonths(-HighlightedFilterMonthsThreshold)
                }
            );

        if (FilterOnlyHighlights.GetValueOrDefault(FilterOnlyHighlightsDefault))
            tmp = tmp
                .Where(r => r.UserRating == null || r.Highlighted)
                .OrderByDescending(r => r.Highlighted)
                .ThenByDescending(r => r.MovieEvent.Movie.ImdbRating)
                .Take(30);

        Records = await tmp.ToListAsync();
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
        await HttpContext.ChallengeAsync("Auth0", new AuthenticationProperties { RedirectUri = returnUrl });
    }

    #region Filter helper functions

    public string GetImageUrl(string local, string remote)
    {
        if (string.IsNullOrEmpty(local))
            return remote;

        return "/images/cache/" + local;
    }

    #endregion
}