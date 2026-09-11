using FxMovies.Core.Commands;
using FxMovies.Core.Entities;
using FxMovies.Core.Queries;
using FxMovies.Core.Services;
using FxMovies.ImdbDB;
using FxMovies.MoviesDB;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace FxMovies.CoreTest;

public class UpdateEpgCommandTest : IDisposable
{
    private const string ChannelCode = "test-channel";

    private readonly SqliteConnection _imdbConnection;
    private readonly ImdbDbContext _imdbDbContext;
    private readonly SqliteConnection _moviesConnection;
    private readonly MoviesDbContext _moviesDbContext;

    public UpdateEpgCommandTest()
    {
        _moviesConnection = new SqliteConnection("DataSource=:memory:");
        _moviesConnection.Open();
        _moviesDbContext = new MoviesDbContext(
            new DbContextOptionsBuilder<MoviesDbContext>().UseSqlite(_moviesConnection).Options);
        _moviesDbContext.Database.EnsureCreated();

        _imdbConnection = new SqliteConnection("DataSource=:memory:");
        _imdbConnection.Open();
        _imdbDbContext = new ImdbDbContext(
            new DbContextOptionsBuilder<ImdbDbContext>().UseSqlite(_imdbConnection).Options);
        _imdbDbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _moviesDbContext.Dispose();
        _moviesConnection.Dispose();
        _imdbDbContext.Dispose();
        _imdbConnection.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     A Humo guide covers a broadcast day: it starts in the early morning and ends in the
    ///     early morning of the day after.  The guides of two consecutive days overlap, and a
    ///     broadcast shortly after midnight is only listed by the guide of the day before.
    /// </summary>
    private static IList<MovieEvent> Guide(DateTime date, Func<DateTime, string> externalId)
    {
        var startTimes = new[]
        {
            date.AddHours(5.5), // also listed by the guide of the day before
            date.AddHours(20),
            date.AddDays(1).AddHours(5.5) // also listed by the guide of the day after
        };

        return startTimes.Select(startTime => new MovieEvent
        {
            ExternalId = externalId(startTime),
            Title = "Test movie",
            Vod = false,
            Feed = MovieEvent.FeedType.Broadcast,
            Type = 1,
            StartTime = startTime,
            EndTime = startTime.AddMinutes(100),
            Duration = 100,
            PosterS = "http://test/poster-s.png",
            PosterM = "http://test/poster-m.png",
            Channel = new Channel
            {
                Code = ChannelCode,
                Name = "Test channel",
                LogoS = "http://test/logo.png"
            }
        }).ToList();
    }

    private UpdateEpgCommand GetCommand(Func<DateTime, string> externalId)
    {
        var updateEpgCommandOptions = new Mock<IOptionsSnapshot<UpdateEpgCommandOptions>>();
        updateEpgCommandOptions.SetupGet(o => o.Value).Returns(new UpdateEpgCommandOptions
        {
            MaxDays = 1,
            DownloadImages = UpdateEpgCommandOptions.DownloadImagesOption.Disabled
        });

        var humoService = new Mock<IHumoService>();
        humoService.Setup(s => s.GetGuide(It.IsAny<DateTime>()))
            .ReturnsAsync((DateTime date) => Guide(date, externalId));

        var imdbMatchingQuery = new Mock<IImdbMatchingQuery>();
        imdbMatchingQuery.Setup(q => q.Execute(It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync(new ImdbMatchingQueryResult());

        return new UpdateEpgCommand(
            NullLogger<UpdateEpgCommand>.Instance,
            _moviesDbContext,
            _imdbDbContext,
            updateEpgCommandOptions.Object,
            new Mock<ITheMovieDbService>().Object,
            new List<IMovieEventService>(),
            humoService.Object,
            imdbMatchingQuery.Object,
            new Mock<IHttpClientFactory>().Object,
            new Mock<IManualMatchesQuery>().Object,
            new Mock<IUpdateImdbLinkCommand>().Object);
    }

    private List<MovieEvent> GetStoredMovieEvents()
    {
        return _moviesDbContext.MovieEvents.Include(me => me.Channel).OrderBy(me => me.StartTime).ToList();
    }

    /// <summary>
    ///     The guide of the last day lists broadcasts that start the day after, which no other
    ///     guide of the same run covers.  Running the grabber again the same day used to store
    ///     those broadcasts a second time.
    /// </summary>
    [Fact]
    public async Task Execute_RepeatedRun_DoesNotDuplicateMovieEvents()
    {
        string ExternalId(DateTime startTime)
        {
            return $"{ChannelCode}-{startTime:yyyyMMddHHmm}";
        }

        await GetCommand(ExternalId).Execute();
        var afterFirstRun = GetStoredMovieEvents();

        await GetCommand(ExternalId).Execute();
        var afterSecondRun = GetStoredMovieEvents();

        Assert.NotEmpty(afterFirstRun);
        Assert.Equal(afterFirstRun.Select(me => me.StartTime), afterSecondRun.Select(me => me.StartTime));
        Assert.Equal(afterSecondRun.Select(me => me.StartTime).Distinct(), afterSecondRun.Select(me => me.StartTime));
    }

    /// <summary>
    ///     A provider can republish a broadcast under a new id.  That is the same broadcast, so
    ///     it should replace the stored one instead of being added next to it.
    /// </summary>
    [Fact]
    public async Task Execute_ChangedExternalIds_DoesNotDuplicateMovieEvents()
    {
        await GetCommand(startTime => $"first-{startTime:yyyyMMddHHmm}").Execute();
        var afterFirstRun = GetStoredMovieEvents();

        await GetCommand(startTime => $"second-{startTime:yyyyMMddHHmm}").Execute();
        var afterSecondRun = GetStoredMovieEvents();

        Assert.NotEmpty(afterFirstRun);
        Assert.Equal(afterFirstRun.Select(me => me.StartTime), afterSecondRun.Select(me => me.StartTime));
        Assert.All(afterSecondRun, me => Assert.StartsWith("second-", me.ExternalId));

        // The stored movie events are updated, not replaced.
        Assert.Equal(afterFirstRun.Select(me => me.Id), afterSecondRun.Select(me => me.Id));
    }

    /// <summary>
    ///     The same movie broadcast more than once is not a duplicate.
    /// </summary>
    [Fact]
    public async Task Execute_SameMovieAtAnotherStartTime_IsKept()
    {
        await GetCommand(startTime => $"{ChannelCode}-{startTime:yyyyMMddHHmm}").Execute();

        var movieEvents = GetStoredMovieEvents();

        Assert.True(movieEvents.Count > 1, $"Expected more than one movie event, got {movieEvents.Count}");
        Assert.All(movieEvents, me => Assert.Equal("Test movie", me.Title));
        Assert.All(movieEvents, me => Assert.Equal(ChannelCode, me.Channel?.Code));
    }

    /// <summary>
    ///     A broadcast that a provider returns twice within the same run is stored once.
    /// </summary>
    [Fact]
    public async Task Execute_DuplicatesInGuide_AreStoredOnce()
    {
        var updateEpgCommandOptions = new Mock<IOptionsSnapshot<UpdateEpgCommandOptions>>();
        updateEpgCommandOptions.SetupGet(o => o.Value).Returns(new UpdateEpgCommandOptions
        {
            MaxDays = 0,
            DownloadImages = UpdateEpgCommandOptions.DownloadImagesOption.Disabled
        });

        var humoService = new Mock<IHumoService>();
        humoService.Setup(s => s.GetGuide(It.IsAny<DateTime>()))
            .ReturnsAsync((DateTime date) =>
            {
                // The same broadcast, returned twice with its own id each time.
                var movieEvents = Guide(date, startTime => $"a-{startTime:yyyyMMddHHmm}");
                foreach (var movieEvent in Guide(date, startTime => $"b-{startTime:yyyyMMddHHmm}"))
                    movieEvents.Add(movieEvent);
                return movieEvents;
            });

        var imdbMatchingQuery = new Mock<IImdbMatchingQuery>();
        imdbMatchingQuery.Setup(q => q.Execute(It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync(new ImdbMatchingQueryResult());

        var command = new UpdateEpgCommand(
            NullLogger<UpdateEpgCommand>.Instance,
            _moviesDbContext,
            _imdbDbContext,
            updateEpgCommandOptions.Object,
            new Mock<ITheMovieDbService>().Object,
            new List<IMovieEventService>(),
            humoService.Object,
            imdbMatchingQuery.Object,
            new Mock<IHttpClientFactory>().Object,
            new Mock<IManualMatchesQuery>().Object,
            new Mock<IUpdateImdbLinkCommand>().Object);

        await command.Execute();

        var movieEvents = GetStoredMovieEvents();

        Assert.NotEmpty(movieEvents);
        Assert.Equal(movieEvents.Select(me => me.StartTime).Distinct(), movieEvents.Select(me => me.StartTime));
        Assert.Single(_moviesDbContext.Channels);
    }
}
