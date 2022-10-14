using EntityFrameworkCoreMock;
using FxMovies.Core.Entities;
using FxMovies.Core.Queries;
using FxMovies.ImdbDB;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using static FxMovies.Core.Entities.MovieEvent;

namespace FxMovies.CoreTest;

public class CachedBroadcastQueryTest
{
    [Fact]
    public async Task TestCached()
    {
        FeedType feedType = FeedType.Broadcast;
        string userId = "u123456";

        Mock<IBroadcastQuery> broadcastQueryMock = new();
        Mock<IMemoryCache> memoryCacheMock = new();
        object cachedBroadcastQueryResult = new BroadcastQueryResult();
        memoryCacheMock.Setup(m => m.TryGetValue(It.IsAny<object>(), out cachedBroadcastQueryResult)).Returns(true);
        Mock<IOptions<CachedBroadcastQueryOptions>> cachedBroadcastQueryOptionsMock = new();
        cachedBroadcastQueryOptionsMock.Setup(o => o.Value).Returns(new CachedBroadcastQueryOptions
        {
            Enable = true
        });
        
        CachedBroadcastQuery cachedBroadcastQuery = new(broadcastQueryMock.Object, memoryCacheMock.Object, cachedBroadcastQueryOptionsMock.Object);

        BroadcastQueryResult result = await cachedBroadcastQuery.Execute(feedType, userId, null, 0, null, 10, 50, 50, true, false);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task TestNotCached()
    {
        FeedType feedType = FeedType.Broadcast;
        string userId = "u123456";

        BroadcastQueryResult broadcastQueryResult = new();

        Mock<IBroadcastQuery> broadcastQueryMock = new();
        broadcastQueryMock.Setup(m => m.Execute(feedType, userId, null, 0, null, 10, 50, 50, true)).ReturnsAsync(broadcastQueryResult);
        Mock<IMemoryCache> memoryCacheMock = new();
        object? cachedBroadcastQueryResult = null;
        Mock<ICacheEntry> cacheEntryMock = new();
        memoryCacheMock.Setup(m => m.TryGetValue(It.IsAny<object>(), out cachedBroadcastQueryResult)).Returns(false);
        memoryCacheMock.Setup(m => m.CreateEntry(It.IsAny<object>())).Returns(cacheEntryMock.Object);
        Mock<IOptions<CachedBroadcastQueryOptions>> cachedBroadcastQueryOptionsMock = new();
        cachedBroadcastQueryOptionsMock.Setup(o => o.Value).Returns(new CachedBroadcastQueryOptions
        {
            Enable = true
        });
        
        CachedBroadcastQuery cachedBroadcastQuery = new(broadcastQueryMock.Object, memoryCacheMock.Object, cachedBroadcastQueryOptionsMock.Object);

        BroadcastQueryResult result = await cachedBroadcastQuery.Execute(feedType, userId, null, 0, null, 10, 50, 50, true, false);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task TestForceNoCache()
    {
        FeedType feedType = FeedType.Broadcast;
        string userId = "u123456";

        BroadcastQueryResult broadcastQueryResult = new();

        Mock<IBroadcastQuery> broadcastQueryMock = new();
        broadcastQueryMock.Setup(m => m.Execute(feedType, userId, null, 0, null, 10, 50, 50, true)).ReturnsAsync(broadcastQueryResult);
        Mock<IMemoryCache> memoryCacheMock = new();
        Mock<ICacheEntry> cacheEntryMock = new();
        Mock<IOptions<CachedBroadcastQueryOptions>> cachedBroadcastQueryOptionsMock = new();
        cachedBroadcastQueryOptionsMock.Setup(o => o.Value).Returns(new CachedBroadcastQueryOptions
        {
            Enable = false
        });
        
        CachedBroadcastQuery cachedBroadcastQuery = new(broadcastQueryMock.Object, memoryCacheMock.Object, cachedBroadcastQueryOptionsMock.Object);

        BroadcastQueryResult result = await cachedBroadcastQuery.Execute(feedType, userId, null, 0, null, 10, 50, 50, true, false);
        Assert.NotNull(result);
    }
}