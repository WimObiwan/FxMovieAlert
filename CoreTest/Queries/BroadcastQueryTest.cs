using EntityFrameworkCoreMock;
using FxMovies.Core.Entities;
using FxMovies.Core.Queries;
using FxMovies.MoviesDB;
using static FxMovies.Core.Entities.MovieEvent;

namespace FxMovies.CoreTest;

public class BroadcastQueryTest
{
    private (DbContextMock<MoviesDbContext>, DbSetMock<MovieEvent>) GetMockDb()
    {
        var user1 = new User
        {
            Id = 1,
            ImdbUserId = "u123456",
            RefreshRequestTime = DateTime.Now,
            LastRefreshRatingsTime = DateTime.Now,
            LastRefreshSuccess = true,
            LastRefreshRatingsResult = "succeeded",
            RefreshCount = 52,
            LastUsageTime = DateTime.Now.AddDays(-7.0),
            Usages = 123,
            WatchListLastRefreshTime = DateTime.Now,
            WatchListLastRefreshSuccess = true,
            WatchListLastRefreshResult = "succeeded"
        };
        Channel channel1 = new()
        {
            Id = 1,
            Code = "C1",
            LogoS = "http://test",
            LogoS_Local = "local",
            Name = "Channel1"
        };
        Movie movie1 = new()
        {
            Id = 1,
            ImdbId = "t123456",
            ImdbRating = 77,
            ImdbVotes = 1234,
            Certification = "US:PG",
            OriginalTitle = "Original title",
            ImdbIgnore = false,
            UserRatings = new List<UserRating>
            {
                new()
                {
                    Id = 1,
                    User = user1,
                    Movie = null,
                    RatingDate = new DateTime(2020, 1, 1),
                    Rating = 70
                }
            },
            UserWatchListItems = new List<UserWatchListItem>()
        };
        Movie movie2 = new()
        {
            Id = 2,
            ImdbId = "t223456",
            ImdbRating = 66,
            ImdbVotes = 1234,
            Certification = "US:PG-13",
            OriginalTitle = "Original title",
            ImdbIgnore = false,
            UserRatings = new List<UserRating>
            {
                new()
                {
                    Id = 2,
                    User = user1,
                    Movie = null,
                    RatingDate = new DateTime(2020, 1, 1),
                    Rating = 80
                }
            },
            UserWatchListItems = new List<UserWatchListItem>()
        };
        Movie movie3 = new()
        {
            Id = 3,
            ImdbId = "t323456",
            ImdbRating = 55,
            ImdbVotes = 1234,
            Certification = "US:R",
            OriginalTitle = "Original title",
            ImdbIgnore = false,
            UserRatings = new List<UserRating>
            {
                new()
                {
                    Id = 3,
                    User = user1,
                    Movie = null,
                    RatingDate = new DateTime(2020, 1, 1),
                    Rating = 40
                }
            },
            UserWatchListItems = new List<UserWatchListItem>()
        };
        var data = new[]
        {
            new MovieEvent
            {
                Id = 1,
                ExternalId = "ext1",
                Type = 1, // 1 = movie, 2 = short movie, 3 = serie
                Vod = false,
                Feed = FeedType.Broadcast,
                StartTime = DateTime.Today.AddDays(3).AddHours(20),
                EndTime = DateTime.Today.AddDays(3).AddHours(22),
                Channel = channel1,
                PosterS = "https://test",
                PosterM = "https://test",
                Duration = 200,
                Genre = "Genre",
                Content = "Content",
                Opinion = "Opinion",
                YeloUrl = "https://test",
                PosterS_Local = "local",
                PosterM_Local = "local",
                VodLink = "https://test",
                AddedTime = DateTime.Now,
                Title = "Star Wars",
                Year = 1969,
                Movie = movie1
            },
            new MovieEvent
            {
                Id = 2,
                ExternalId = "ext2",
                Type = 1, // 1 = movie, 2 = short movie, 3 = serie
                Vod = true,
                Feed = FeedType.FreeVod,
                StartTime = DateTime.Today.AddDays(3).AddHours(20),
                EndTime = DateTime.Today.AddDays(3).AddHours(22),
                Channel = channel1,
                PosterS = "https://test",
                PosterM = "https://test",
                Duration = 200,
                Genre = "Genre",
                Content = "Content",
                Opinion = "Opinion",
                YeloUrl = "https://test",
                PosterS_Local = "local",
                PosterM_Local = "local",
                VodLink = "https://test",
                AddedTime = DateTime.Now,
                Title = "Back to the future",
                Year = 1969,
                Movie = movie2
            },
            new MovieEvent
            {
                Id = 3,
                ExternalId = "ext3",
                Type = 1, // 1 = movie, 2 = short movie, 3 = serie
                Vod = true,
                Feed = FeedType.PaidVod,
                StartTime = DateTime.Today.AddDays(3).AddHours(20),
                EndTime = DateTime.Today.AddDays(3).AddHours(22),
                Channel = channel1,
                PosterS = "https://test",
                PosterM = "https://test",
                Duration = 200,
                Genre = "Genre",
                Content = "Content",
                Opinion = "Opinion",
                YeloUrl = "https://test",
                PosterS_Local = "local",
                PosterM_Local = "local",
                VodLink = "https://test",
                AddedTime = DateTime.Now,
                Title = "Indiana Jones",
                Year = 1979,
                Movie = movie3
            }
        };

        var moviesDbContextMock = new DbContextMock<MoviesDbContext>(Util.DummyMoviesDbOptions);
        var movieEventsDbSetMock = moviesDbContextMock.CreateDbSetMock(x => x.MovieEvents, (x, _) => x, data);

        return (moviesDbContextMock, movieEventsDbSetMock);
    }

    [Fact]
    public async Task TestBroadcast()
    {
        var (moviesDbContextMock, movieEventsDbSetMock) = GetMockDb();

        var feedType = FeedType.Broadcast;
        var userId = "u123456";

        BroadcastQuery broadcastQuery = new(moviesDbContextMock.Object);

        var result = await broadcastQuery.Execute(feedType, userId, null, 1, null, 10, 50, 50, true);
        Assert.NotNull(result);
        Assert.Equal(1, result.Count);
        Assert.Equal(1, result.CountTypeFilm);
        Assert.Equal(0, result.CountTypeShort);
        Assert.Equal(0, result.CountTypeSerie);
        Assert.Equal(1, result.CountMinRating5);
        Assert.Equal(1, result.CountMinRating6);
        Assert.Equal(1, result.CountMinRating65);
        Assert.Equal(1, result.CountMinRating7);
        Assert.Equal(0, result.CountMinRating8);
        Assert.Equal(0, result.CountMinRating9);
        Assert.Equal(0, result.CountNotOnImdb);
        Assert.Equal(0, result.CountNotRatedOnImdb);
        Assert.Equal(0, result.CountCertNone);
        Assert.Equal(0, result.CountCertG);
        Assert.Equal(1, result.CountCertPG);
        Assert.Equal(0, result.CountCertPG13);
        Assert.Equal(0, result.CountCertR);
        Assert.Equal(0, result.CountCertNC17);
        Assert.Equal(0, result.CountCertOther);
        Assert.Equal(0, result.CountRated);
        Assert.Equal(1, result.CountNotYetRated);
        Assert.Equal(1, result.Count3days);
        Assert.Equal(1, result.Count5days);
        Assert.Equal(1, result.Count8days);

        Assert.NotNull(result.Records);
        Assert.Single(result.Records);

        var record = result.Records.Single();
        Assert.False(record.Highlighted);
        Assert.Equal(1, record.MovieEvent.Id);

        Assert.Null(result.MovieEvent);

        Assert.False(result.CacheEnabled);
        Assert.False(result.CacheUsed);
        //public DateTime QueryDateTime;
        //public TimeSpan QueryDuration;
    }

    [Fact]
    public async Task TestBroadcastWithMinRating50()
    {
        var (moviesDbContextMock, movieEventsDbSetMock) = GetMockDb();

        var feedType = FeedType.Broadcast;
        var userId = "u123456";

        BroadcastQuery broadcastQuery = new(moviesDbContextMock.Object);

        var result = await broadcastQuery.Execute(feedType, userId, null, 1, 5.0M, 10, 50, 50, true);
        Assert.NotNull(result);
        Assert.Equal(1, result.Count);
        Assert.Equal(1, result.CountTypeFilm);
        Assert.Equal(0, result.CountTypeShort);
        Assert.Equal(0, result.CountTypeSerie);
        Assert.Equal(1, result.CountMinRating5);
        Assert.Equal(1, result.CountMinRating6);
        Assert.Equal(1, result.CountMinRating65);
        Assert.Equal(1, result.CountMinRating7);
        Assert.Equal(0, result.CountMinRating8);
        Assert.Equal(0, result.CountMinRating9);
        Assert.Equal(0, result.CountNotOnImdb);
        Assert.Equal(0, result.CountNotRatedOnImdb);
        Assert.Equal(0, result.CountCertNone);
        Assert.Equal(0, result.CountCertG);
        Assert.Equal(1, result.CountCertPG);
        Assert.Equal(0, result.CountCertPG13);
        Assert.Equal(0, result.CountCertR);
        Assert.Equal(0, result.CountCertNC17);
        Assert.Equal(0, result.CountCertOther);
        Assert.Equal(0, result.CountRated);
        Assert.Equal(1, result.CountNotYetRated);
        Assert.Equal(1, result.Count3days);
        Assert.Equal(1, result.Count5days);
        Assert.Equal(1, result.Count8days);

        Assert.NotNull(result.Records);
        Assert.Single(result.Records);

        var record = result.Records.Single();
        Assert.False(record.Highlighted);
        Assert.Equal(1, record.MovieEvent.Id);

        Assert.Null(result.MovieEvent);

        Assert.False(result.CacheEnabled);
        Assert.False(result.CacheUsed);
        //public DateTime QueryDateTime;
        //public TimeSpan QueryDuration;
    }

    [Fact]
    public async Task TestBroadcastWithMinRating90()
    {
        var (moviesDbContextMock, movieEventsDbSetMock) = GetMockDb();

        var feedType = FeedType.Broadcast;
        var userId = "u123456";

        BroadcastQuery broadcastQuery = new(moviesDbContextMock.Object);

        var result = await broadcastQuery.Execute(feedType, userId, null, 1, 9.0M, 10, 50, 50, true);
        Assert.NotNull(result);
        Assert.Equal(1, result.Count);
        Assert.Equal(1, result.CountTypeFilm);
        Assert.Equal(0, result.CountTypeShort);
        Assert.Equal(0, result.CountTypeSerie);
        Assert.Equal(1, result.CountMinRating5);
        Assert.Equal(1, result.CountMinRating6);
        Assert.Equal(1, result.CountMinRating65);
        Assert.Equal(1, result.CountMinRating7);
        Assert.Equal(0, result.CountMinRating8);
        Assert.Equal(0, result.CountMinRating9);
        Assert.Equal(0, result.CountNotOnImdb);
        Assert.Equal(0, result.CountNotRatedOnImdb);
        Assert.Equal(0, result.CountCertNone);
        Assert.Equal(0, result.CountCertG);
        Assert.Equal(1, result.CountCertPG);
        Assert.Equal(0, result.CountCertPG13);
        Assert.Equal(0, result.CountCertR);
        Assert.Equal(0, result.CountCertNC17);
        Assert.Equal(0, result.CountCertOther);
        Assert.Equal(0, result.CountRated);
        Assert.Equal(1, result.CountNotYetRated);
        Assert.Equal(1, result.Count3days);
        Assert.Equal(1, result.Count5days);
        Assert.Equal(1, result.Count8days);

        Assert.NotNull(result.Records);
        Assert.Empty(result.Records);

        // var record = result.Records.Single();
        // Assert.False(record.Highlighted);
        // Assert.Equal(1, record.MovieEvent.Id);

        Assert.Null(result.MovieEvent);

        Assert.False(result.CacheEnabled);
        Assert.False(result.CacheUsed);
        //public DateTime QueryDateTime;
        //public TimeSpan QueryDuration;
    }

    [Fact]
    public async Task TestFreeVod()
    {
        var (moviesDbContextMock, movieEventsDbSetMock) = GetMockDb();

        var feedType = FeedType.FreeVod;
        var userId = "u123456";

        BroadcastQuery broadcastQuery = new(moviesDbContextMock.Object);

        var result = await broadcastQuery.Execute(feedType, userId, null, 1, null, 10, 50, 50, true);
        Assert.NotNull(result);
        Assert.Equal(1, result.Count);
        Assert.Equal(1, result.CountTypeFilm);
        Assert.Equal(0, result.CountTypeShort);
        Assert.Equal(0, result.CountTypeSerie);
        Assert.Equal(1, result.CountMinRating5);
        Assert.Equal(1, result.CountMinRating6);
        Assert.Equal(1, result.CountMinRating65);
        Assert.Equal(0, result.CountMinRating7);
        Assert.Equal(0, result.CountMinRating8);
        Assert.Equal(0, result.CountMinRating9);
        Assert.Equal(0, result.CountNotOnImdb);
        Assert.Equal(0, result.CountNotRatedOnImdb);
        Assert.Equal(0, result.CountCertNone);
        Assert.Equal(0, result.CountCertG);
        Assert.Equal(0, result.CountCertPG);
        Assert.Equal(1, result.CountCertPG13);
        Assert.Equal(0, result.CountCertR);
        Assert.Equal(0, result.CountCertNC17);
        Assert.Equal(0, result.CountCertOther);
        Assert.Equal(0, result.CountRated);
        Assert.Equal(1, result.CountNotYetRated);
        Assert.Equal(1, result.Count3days);
        Assert.Equal(1, result.Count5days);
        Assert.Equal(1, result.Count8days);

        Assert.NotNull(result.Records);
        Assert.Single(result.Records);

        var record = result.Records.Single();
        Assert.False(record.Highlighted);
        Assert.Equal(2, record.MovieEvent.Id);

        Assert.Null(result.MovieEvent);

        Assert.False(result.CacheEnabled);
        Assert.False(result.CacheUsed);
        //public DateTime QueryDateTime;
        //public TimeSpan QueryDuration;
    }

    [Fact]
    public async Task TestPaidVod()
    {
        var (moviesDbContextMock, movieEventsDbSetMock) = GetMockDb();

        var feedType = FeedType.PaidVod;
        var userId = "u123456";

        BroadcastQuery broadcastQuery = new(moviesDbContextMock.Object);

        var result = await broadcastQuery.Execute(feedType, userId, null, 1, null, 10, 50, 50, true);
        Assert.NotNull(result);
        Assert.Equal(1, result.Count);
        Assert.Equal(1, result.CountTypeFilm);
        Assert.Equal(0, result.CountTypeShort);
        Assert.Equal(0, result.CountTypeSerie);
        Assert.Equal(1, result.CountMinRating5);
        Assert.Equal(0, result.CountMinRating6);
        Assert.Equal(0, result.CountMinRating65);
        Assert.Equal(0, result.CountMinRating7);
        Assert.Equal(0, result.CountMinRating8);
        Assert.Equal(0, result.CountMinRating9);
        Assert.Equal(0, result.CountNotOnImdb);
        Assert.Equal(0, result.CountNotRatedOnImdb);
        Assert.Equal(0, result.CountCertNone);
        Assert.Equal(0, result.CountCertG);
        Assert.Equal(0, result.CountCertPG);
        Assert.Equal(0, result.CountCertPG13);
        Assert.Equal(1, result.CountCertR);
        Assert.Equal(0, result.CountCertNC17);
        Assert.Equal(0, result.CountCertOther);
        Assert.Equal(0, result.CountRated);
        Assert.Equal(1, result.CountNotYetRated);
        Assert.Equal(1, result.Count3days);
        Assert.Equal(1, result.Count5days);
        Assert.Equal(1, result.Count8days);

        Assert.NotNull(result.Records);
        Assert.Single(result.Records);

        var record = result.Records.Single();
        Assert.False(record.Highlighted);
        Assert.Equal(3, record.MovieEvent.Id);

        Assert.Null(result.MovieEvent);

        Assert.False(result.CacheEnabled);
        Assert.False(result.CacheUsed);
        //public DateTime QueryDateTime;
        //public TimeSpan QueryDuration;
    }

    [Fact]
    public async Task TestPaidVodWithSingleMovie()
    {
        var (moviesDbContextMock, movieEventsDbSetMock) = GetMockDb();

        var feedType = FeedType.PaidVod;
        var userId = "u123456";

        BroadcastQuery broadcastQuery = new(moviesDbContextMock.Object);

        var result = await broadcastQuery.Execute(feedType, userId, 3, 1, null, 10, 50, 50, true);
        Assert.NotNull(result);
        Assert.Equal(1, result.Count);
        Assert.Equal(1, result.CountTypeFilm);
        Assert.Equal(0, result.CountTypeShort);
        Assert.Equal(0, result.CountTypeSerie);
        Assert.Equal(1, result.CountMinRating5);
        Assert.Equal(0, result.CountMinRating6);
        Assert.Equal(0, result.CountMinRating65);
        Assert.Equal(0, result.CountMinRating7);
        Assert.Equal(0, result.CountMinRating8);
        Assert.Equal(0, result.CountMinRating9);
        Assert.Equal(0, result.CountNotOnImdb);
        Assert.Equal(0, result.CountNotRatedOnImdb);
        Assert.Equal(0, result.CountCertNone);
        Assert.Equal(0, result.CountCertG);
        Assert.Equal(0, result.CountCertPG);
        Assert.Equal(0, result.CountCertPG13);
        Assert.Equal(1, result.CountCertR);
        Assert.Equal(0, result.CountCertNC17);
        Assert.Equal(0, result.CountCertOther);
        Assert.Equal(0, result.CountRated);
        Assert.Equal(1, result.CountNotYetRated);
        Assert.Equal(1, result.Count3days);
        Assert.Equal(1, result.Count5days);
        Assert.Equal(1, result.Count8days);

        Assert.NotNull(result.Records);
        Assert.Single(result.Records);

        var record = result.Records.Single();
        Assert.False(record.Highlighted);
        Assert.Equal(3, record.MovieEvent.Id);

        Assert.NotNull(result.MovieEvent);
        Assert.Equal(3, result.MovieEvent!.Id);

        Assert.False(result.CacheEnabled);
        Assert.False(result.CacheUsed);
        //public DateTime QueryDateTime;
        //public TimeSpan QueryDuration;
    }
}