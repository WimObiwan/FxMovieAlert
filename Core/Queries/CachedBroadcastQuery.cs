using System;
using System.Threading.Tasks;
using FxMovies.Core.Entities;
using Microsoft.Extensions.Caching.Memory;

namespace FxMovies.Core.Queries;

public interface ICachedBroadcastQuery
{
    Task<BroadcastQueryResult> Execute(MovieEvent.FeedType feed, string userId, int? m,
        int filterTypeMask, decimal? filterMinRating, int filterMaxDays, 
        int highlightedFilterRatingThreshold, int highlightedFilterMonthsThreshold, bool filterOnlyHighlights);
}

public class CachedBroadcastQuery : ICachedBroadcastQuery
{
    private readonly IBroadcastQuery _broadcastQuery;
    private readonly IMemoryCache _memoryCache;

    public CachedBroadcastQuery(IBroadcastQuery broadcastQuery, IMemoryCache memoryCache)
    {
        _broadcastQuery = broadcastQuery;
        _memoryCache = memoryCache;
    }

    public async Task<BroadcastQueryResult> Execute(MovieEvent.FeedType feed, string userId, int? m,
        int filterTypeMask, decimal? filterMinRating, int filterMaxDays, 
        int highlightedFilterRatingThreshold, int highlightedFilterMonthsThreshold, bool filterOnlyHighlights)
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

        return await _memoryCache.GetOrCreateAsync(hashCodeValue, (cacheEntry) => 
            {
                cacheEntry.SlidingExpiration = TimeSpan.FromSeconds(60);
                return _broadcastQuery.Execute(feed, userId, m, filterTypeMask, filterMinRating, filterMaxDays, 
                    highlightedFilterRatingThreshold, highlightedFilterMonthsThreshold, filterOnlyHighlights);
            });
   }
}