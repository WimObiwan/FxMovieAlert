using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FxMovies.Core;
using FxMovies.Core.Entities;
using FxMovies.Core.Queries;
using FxMovies.Core.Repositories;
using FxMovies.MoviesDB;
using FxMovies.Site.Components;
using FxMovies.Site.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FxMovies.Site.Pages;

public class BroadcastsModelBase : PageModel, IFilterBarParentModel
{
    private readonly MovieEvent.FeedType _feed;
    private readonly MoviesDbContext _moviesDbContext;
    private readonly SiteOptions _siteOptions;

    private readonly IBroadcastQuery _broadcastQuery;
    private readonly IUsersRepository _usersRepository;

    //public int AdsInterval = 5;
    public bool EditImdbLinks;

    //public bool? LastRefreshSuccess;
    public MovieEvent MovieEvent => Data?.MovieEvent;
    public IList<Record> Records => Data?.Records ?? new List<Record>();

    public BroadcastsModelBase(
        MovieEvent.FeedType feed,
        IOptions<SiteOptions> siteOptions,
        MoviesDbContext moviesDbContext,
        IBroadcastQuery broadcastQuery,
        IUsersRepository usersRepository)
    {
        _feed = feed;
        _siteOptions = siteOptions.Value;
        _moviesDbContext = moviesDbContext;
        _broadcastQuery = broadcastQuery;
        _usersRepository = usersRepository;
    }

    private int HighlightedFilterMonthsThreshold { get; } = 36;
    private int HighlightedFilterRatingThreshold { get; } = 8;
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

    public BroadcastQueryResult Data { get; private set; }

    public async Task OnGet(int? m = null, bool? onlyHighlights = null, int? typeMask = null, decimal? minrating = null,
        bool? notyetrated = null, Cert cert = Cert.all, int? maxdays = null)
    {
        var userId = ClaimChecker.UserId(User.Identity);

        //AdsInterval = _siteOptions.AdsInterval;
        FilterMaxDaysDefault = _siteOptions.DefaultMaxDays;
        FilterMaxDays = FilterMaxDaysDefault;
        FilterTypeMask = FilterTypeMaskDefault;
        FilterOnlyHighlights = onlyHighlights;

        EditImdbLinks = ClaimChecker.Has(User.Identity, Claims.EditImdbLinks);

        if (typeMask.HasValue)
            FilterTypeMask = typeMask.Value;

        if (minrating.HasValue)
            FilterMinRating = minrating.Value;

        // Only allow setting more days when authenticated
        if (maxdays.HasValue && (User.Identity?.IsAuthenticated ?? false))
            FilterMaxDays = maxdays.Value;

        FilterNotYetRated = notyetrated;
        FilterCert = cert & Cert.all2;
        if (FilterCert == Cert.all2)
            FilterCert = Cert.all;

        if (userId != null)
        {
            var userResult = await _usersRepository.UpdateUserLastUsedAndGetData(userId);
            if (userResult != null)
            {
                RefreshRequestTime = userResult.RefreshRequestTime;
                LastRefreshRatingsTime = userResult.LastRefreshRatingsTime;
                //LastRefreshSuccess = result.LastRefreshSuccess;
                ImdbUserId = userResult.ImdbUserId;
            }
        }

        if (m.HasValue && m.Value == -2) throw new Exception("Sentry test exception");

        Data = await _broadcastQuery.Execute(_feed, userId, m, FilterTypeMask, FilterMinRating, FilterMaxDays, HighlightedFilterRatingThreshold,
            HighlightedFilterMonthsThreshold, FilterOnlyHighlights.GetValueOrDefault(FilterOnlyHighlightsDefault));

        if (Data.MovieEvent != null)
        {
            var days = (int)(Data.MovieEvent.StartTime.Date - DateTime.Now.Date).TotalDays;
            if (FilterMaxDays != 0 && FilterMaxDays < days)
                FilterMaxDays = days;
        }
    }

    // private static Cert ParseCertification(string certification)
    // {
    //     switch (certification)
    //     {
    //         case null:
    //         case "":
    //             return Cert.none;
    //         case "US:G":
    //             return Cert.g;
    //         case "US:PG":
    //             return Cert.pg;
    //         case "US:PG-13":
    //             return Cert.pg13;
    //         case "US:R":
    //             return Cert.r;
    //         case "US:NC-17":
    //             return Cert.nc17;
    //         default:
    //             return Cert.other;
    //     }
    // }

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