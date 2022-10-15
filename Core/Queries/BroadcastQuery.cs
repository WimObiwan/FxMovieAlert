using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using FxMovies.Core.Entities;
using FxMovies.MoviesDB;
using Microsoft.EntityFrameworkCore;

namespace FxMovies.Core.Queries;

public interface IBroadcastQuery
{
    Task<BroadcastQueryResult> Execute(MovieEvent.FeedType feed, string? userId, int? m,
        int filterTypeMask, decimal? filterMinRating, int filterMaxDays,
        int highlightedFilterRatingThreshold, int highlightedFilterMonthsThreshold, bool filterOnlyHighlights);
}

[ExcludeFromCodeCoverage]
public class BroadcastQueryResult
{
    public bool CacheEnabled;
    public bool CacheUsed;
    public int Count;
    public int Count3days;
    public int Count5days;
    public int Count8days;
    public int CountCertG;
    public int CountCertNc17;
    public int CountCertNone;
    public int CountCertOther;
    public int CountCertPg;
    public int CountCertPg13;
    public int CountCertR;
    public int CountMinRating5;
    public int CountMinRating6;
    public int CountMinRating65;
    public int CountMinRating7;
    public int CountMinRating8;
    public int CountMinRating9;
    public int CountNotOnImdb;
    public int CountNotRatedOnImdb;
    public int CountNotYetRated;
    public int CountRated;
    public int CountTypeFilm;
    public int CountTypeSerie;
    public int CountTypeShort;

    public MovieEvent? MovieEvent;
    public DateTime QueryDateTime;
    public TimeSpan QueryDuration;

    public List<Record> Records = default!;
}

[ExcludeFromCodeCoverage]
public class Record
{
    public MovieEvent MovieEvent { get; init; } = default!;
    public UserRating? UserRating { get; init; }
    public UserWatchListItem? UserWatchListItem { get; init; }
    public bool Highlighted { get; init; }
}

public class BroadcastQuery : IBroadcastQuery
{
    private readonly MoviesDbContext _moviesDbContext;

    public BroadcastQuery(MoviesDbContext moviesDbContext)
    {
        _moviesDbContext = moviesDbContext;
    }

    public async Task<BroadcastQueryResult> Execute(MovieEvent.FeedType feed, string? userId, int? m,
        int filterTypeMask, decimal? filterMinRating, int filterMaxDays,
        int highlightedFilterRatingThreshold, int highlightedFilterMonthsThreshold, bool filterOnlyHighlights)
    {
        var stopwatch = Stopwatch.StartNew();
        BroadcastQueryResult result = new();

        var now = DateTime.Now;

        bool? streaming;
        switch (feed)
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
            _moviesDbContext.MovieEvents.Where(me => me.Feed == feed || (me.Feed == null && me.Vod == streaming));

        if (feed == MovieEvent.FeedType.Broadcast)
            dbMovieEvents = dbMovieEvents.Where(me =>
                (filterMaxDays == 0 || me.StartTime.Date <= now.Date.AddDays(filterMaxDays)) && me.EndTime >= now &&
                me.StartTime >= now.AddMinutes(-30));
        else
            dbMovieEvents = dbMovieEvents.Where(me => me.EndTime == null || me.EndTime >= now);

        result.Count = await dbMovieEvents.CountAsync();
        result.CountTypeFilm = await dbMovieEvents.Where(me => me.Type == 1).CountAsync();
        result.CountTypeShort = await dbMovieEvents.Where(me => me.Type == 2).CountAsync();
        result.CountTypeSerie = await dbMovieEvents.Where(me => me.Type == 3).CountAsync();
        result.CountMinRating5 = await dbMovieEvents.Where(me => me.Movie!.ImdbRating >= 50).CountAsync();
        result.CountMinRating6 = await dbMovieEvents.Where(me => me.Movie!.ImdbRating >= 60).CountAsync();
        result.CountMinRating65 = await dbMovieEvents.Where(me => me.Movie!.ImdbRating >= 65).CountAsync();
        result.CountMinRating7 = await dbMovieEvents.Where(me => me.Movie!.ImdbRating >= 70).CountAsync();
        result.CountMinRating8 = await dbMovieEvents.Where(me => me.Movie!.ImdbRating >= 80).CountAsync();
        result.CountMinRating9 = await dbMovieEvents.Where(me => me.Movie!.ImdbRating >= 90).CountAsync();
        result.CountNotOnImdb = await dbMovieEvents
            .Where(me => me.Movie == null || (string.IsNullOrEmpty(me.Movie.ImdbId) && !me.Movie.ImdbIgnore))
            .CountAsync();
        result.CountNotRatedOnImdb = await dbMovieEvents.Where(me => me.Movie!.ImdbRating == null).CountAsync();
        result.CountCertNone =
            await dbMovieEvents.Where(me => string.IsNullOrEmpty(me.Movie!.Certification)).CountAsync();
        result.CountCertG = await dbMovieEvents.Where(me => me.Movie!.Certification == "US:G").CountAsync();
        result.CountCertPg = await dbMovieEvents.Where(me => me.Movie!.Certification == "US:PG").CountAsync();
        result.CountCertPg13 = await dbMovieEvents.Where(me => me.Movie!.Certification == "US:PG-13").CountAsync();
        result.CountCertR = await dbMovieEvents.Where(me => me.Movie!.Certification == "US:R").CountAsync();
        result.CountCertNc17 = await dbMovieEvents.Where(me => me.Movie!.Certification == "US:NC-17").CountAsync();
        result.CountCertOther = result.Count - result.CountCertNone - result.CountCertG - result.CountCertPg -
                                result.CountCertPg13 - result.CountCertR - result.CountCertNc17;
        result.CountRated = await dbMovieEvents.CountAsync(me =>
            me.Movie!.UserRatings.Any(ur => ur.User != null && ur.User.UserId == userId));
        result.CountNotYetRated = result.Count - result.CountRated;
        result.Count3days = await dbMovieEvents.Where(me => me.StartTime.Date <= now.Date.AddDays(3)).CountAsync();
        result.Count5days = await dbMovieEvents.Where(me => me.StartTime.Date <= now.Date.AddDays(5)).CountAsync();
        result.Count8days = await dbMovieEvents.Where(me => me.StartTime.Date <= now.Date.AddDays(8)).CountAsync();

        var tmp = dbMovieEvents
            .Where(me =>
                    (
                        ((filterTypeMask & 1) == 1 && me.Type == 1)
                        || ((filterTypeMask & 2) == 2 && me.Type == 2)
                        || ((filterTypeMask & 4) == 4 && me.Type == 3)
                    )
                    &&
                    (!filterMinRating.HasValue
                     || (filterMinRating.Value == Constants.NO_IMDB_ID && (me.Movie == null ||
                                                                           (string.IsNullOrEmpty(me.Movie.ImdbId) &&
                                                                            !me.Movie.ImdbIgnore)))
                     || (filterMinRating.Value == Constants.NO_IMDB_RATING && me.Movie!.ImdbRating == null)
                     || (filterMinRating.Value >= 0.0m && me.Movie!.ImdbRating >= filterMinRating.Value * 10))
                // && 
                // (FilterCert == Cert.all || (ParseCertification(me.Movie.Certification) & FilterCert) != 0)
            )
            .AsNoTracking()
            .Include(me => me.Channel)
            .Include(me => me.Movie)
            .Select(me => new
            {
                MovieEvent = me,
                UserRating = me.Movie!.UserRatings.FirstOrDefault(ur => ur.User!.UserId == userId),
                UserWatchListItem = me.Movie!.UserWatchListItems.FirstOrDefault(ur => ur.User!.UserId == userId)
            })
            .Select(r => new Record
                {
                    MovieEvent = r.MovieEvent,
                    UserRating = r.UserRating,
                    UserWatchListItem = r.UserWatchListItem,
                    Highlighted =
                        r.UserWatchListItem != null
                        ||
                        (r.UserRating != null
                         && r.UserRating.Rating >= highlightedFilterRatingThreshold
                         && r.UserRating.RatingDate < now.AddMonths(-highlightedFilterMonthsThreshold))
                }
            );

        if (filterOnlyHighlights)
            tmp = tmp
                .Where(r => r.UserRating == null || r.Highlighted)
                .OrderByDescending(r => r.Highlighted)
                .ThenByDescending(r => r.MovieEvent.Movie!.ImdbRating)
                .Take(30);

        result.Records = await tmp.ToListAsync();

        if (m.HasValue)
            result.MovieEvent = await dbMovieEvents
                .Include(me => me.Movie)
                .Include(me => me.Channel)
                .SingleOrDefaultAsync(me => me.Id == m.Value);

        result.QueryDateTime = DateTime.UtcNow;
        result.QueryDuration = stopwatch.Elapsed;

        return result;
    }
}