using System;
using System.Threading.Tasks;
using FxMovies.Core.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace FxMovies.Core.Queries;

public interface ICachedBroadcastQuery
{
    Task<BroadcastQueryResult> Execute(MovieEvent.FeedType feed, string userId, int? m,
        int filterTypeMask, decimal? filterMinRating, int filterMaxDays, 
        int highlightedFilterRatingThreshold, int highlightedFilterMonthsThreshold, bool filterOnlyHighlights);
}

public class CachedBroadcastQueryOptions
{
    public static string Position => "CachedBroadcastQuery";

    public bool Enable { get; set; }
    public double AbsoluteExpirationSeconds { get; set; }
    public double SlidingExpirationSeconds { get; set; }
}

public class CachedBroadcastQuery : ICachedBroadcastQuery
{
    private readonly IBroadcastQuery _broadcastQuery;
    private readonly IMemoryCache _memoryCache;
    private readonly CachedBroadcastQueryOptions _options;

    public CachedBroadcastQuery(IBroadcastQuery broadcastQuery, IMemoryCache memoryCache, IOptions<CachedBroadcastQueryOptions> options)
    {
        _broadcastQuery = broadcastQuery;
        _memoryCache = memoryCache;
        _options = options.Value;
    }

    public async Task<BroadcastQueryResult> Execute(MovieEvent.FeedType feed, string userId, int? m,
        int filterTypeMask, decimal? filterMinRating, int filterMaxDays, 
        int highlightedFilterRatingThreshold, int highlightedFilterMonthsThreshold, bool filterOnlyHighlights)
    {
        Task<BroadcastQueryResult> Fn()
        {
            return _broadcastQuery.Execute(feed, userId, m, filterTypeMask, filterMinRating, filterMaxDays, 
                    highlightedFilterRatingThreshold, highlightedFilterMonthsThreshold, filterOnlyHighlights);
        }

        if (_options.Enable)
        {
            HashCode hashCode = new();
            hashCode.Add(feed);
            hashCode.Add(userId);
            hashCode.Add(m);
            hashCode.Add(filterTypeMask);
            hashCode.Add(filterMinRating);
            hashCode.Add(filterMaxDays);
            hashCode.Add(highlightedFilterRatingThreshold);
            hashCode.Add(highlightedFilterMonthsThreshold);
            hashCode.Add(filterOnlyHighlights);
            int hashCodeValue = hashCode.ToHashCode();

            return await _memoryCache.GetOrCreateAsync(hashCodeValue, async (cacheEntry) => 
                {
                    cacheEntry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_options.AbsoluteExpirationSeconds);
                    cacheEntry.SlidingExpiration = TimeSpan.FromSeconds(_options.SlidingExpirationSeconds);
                    return await Fn();
                });
        }
        else
        {
            return await Fn();
        }
   }
}