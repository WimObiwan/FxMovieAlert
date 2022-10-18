using EntityFrameworkCoreMock;
using FxMovies.Core;
using FxMovies.Core.Entities;
using FxMovies.Core.Services;
using FxMovies.ImdbDB;
using FxMovies.MoviesDB;
using Moq;

namespace FxMovies.CoreTest;

public class MovieCreationHelperTest
{
    private (DbContextMock<MoviesDbContext>, DbSetMock<Movie>) GetMoviesDbContextMock()
    {
        var movies = new[]
        {
            new()
            {
                Id = 1,
                ImdbId = "123456",
                ImdbRating = 77,
                ImdbVotes = 5555,
                Certification = "US:PG-13",
                OriginalTitle = "Back to the future",
                ImdbIgnore = false
            },
            new Movie
            {
                Id = 2,
                ImdbId = null,
                ImdbRating = null,
                ImdbVotes = null,
                Certification = null,
                OriginalTitle = "The hunt for Red October",
                ImdbIgnore = false
            }
        };

        var moviesDbContextMock = new DbContextMock<MoviesDbContext>(Util.DummyMoviesDbOptions);
        var movieEventsDbSetMock = moviesDbContextMock.CreateDbSetMock(x => x.Movies, (x, _) => x, movies);

        return (moviesDbContextMock, movieEventsDbSetMock);
    }

    private (DbContextMock<ImdbDbContext>, DbSetMock<ImdbMovie>) GetImdbDbContextMock()
    {
        var imdbMovies = new ImdbMovie[]
        {
            new()
            {
                Id = 1,
                ImdbId = "654321",
                PrimaryTitle = "Star Wars",
                Rating = 88,
                Votes = 66666,
                Year = 1976
            }
        };

        var imdbDbContextMock = new DbContextMock<ImdbDbContext>(Util.DummyImdbDbOptions);
        var imdbMoviesDbSetMock = imdbDbContextMock.CreateDbSetMock(x => x.Movies, (x, _) => x, imdbMovies);

        return (imdbDbContextMock, imdbMoviesDbSetMock);
    }

    [Fact]
    public async Task TestExisting()
    {
        var (moviesDbContextMock, movieEventsDbSetMock) = GetMoviesDbContextMock();
        var (imdbDbContextMock, imdbMoviesDbSetMock) = GetImdbDbContextMock();

        Mock<ITheMovieDbService> theMovieDbServiceMock = new();

        MovieCreationHelper movieCreationHelper =
            new(moviesDbContextMock.Object, imdbDbContextMock.Object, theMovieDbServiceMock.Object);
        var result = await movieCreationHelper.GetOrCreateMovieByImdbId("123456");

        theMovieDbServiceMock.VerifyNoOtherCalls();

        Assert.Equal(1, result.Id);
        Assert.Equal("123456", result.ImdbId);
        Assert.Equal(77, result.ImdbRating);
        Assert.Equal(5555, result.ImdbVotes);
        Assert.Equal("US:PG-13", result.Certification);
        Assert.Equal("Back to the future", result.OriginalTitle);
        Assert.False(result.ImdbIgnore);
    }

    [Fact]
    public async Task TestExistingWithRefresh()
    {
        var (moviesDbContextMock, movieEventsDbSetMock) = GetMoviesDbContextMock();
        var (imdbDbContextMock, imdbMoviesDbSetMock) = GetImdbDbContextMock();

        Mock<ITheMovieDbService> theMovieDbServiceMock = new();

        MovieCreationHelper movieCreationHelper =
            new(moviesDbContextMock.Object, imdbDbContextMock.Object, theMovieDbServiceMock.Object);
        var result = await movieCreationHelper.GetOrCreateMovieByImdbId("123456", true);

        theMovieDbServiceMock.VerifyNoOtherCalls();

        Assert.Equal(1, result.Id);
        Assert.Equal("123456", result.ImdbId);
        Assert.Equal(77, result.ImdbRating);
        Assert.Equal(5555, result.ImdbVotes);
        Assert.Equal("US:PG-13", result.Certification);
        Assert.Equal("Back to the future", result.OriginalTitle);
        Assert.False(result.ImdbIgnore);
    }

    [Fact]
    public async Task TestNonExisting()
    {
        var (moviesDbContextMock, movieEventsDbSetMock) = GetMoviesDbContextMock();
        var (imdbDbContextMock, imdbMoviesDbSetMock) = GetImdbDbContextMock();

        Mock<ITheMovieDbService> theMovieDbServiceMock = new();

        MovieCreationHelper movieCreationHelper =
            new(moviesDbContextMock.Object, imdbDbContextMock.Object, theMovieDbServiceMock.Object);
        var result = await movieCreationHelper.GetOrCreateMovieByImdbId("654321");

        theMovieDbServiceMock.VerifyNoOtherCalls();

        Assert.Equal("654321", result.ImdbId);
    }

    [Fact]
    public async Task TestNonExistingWithRefresh1()
    {
        var (moviesDbContextMock, movieEventsDbSetMock) = GetMoviesDbContextMock();
        var (imdbDbContextMock, imdbMoviesDbSetMock) = GetImdbDbContextMock();

        Mock<ITheMovieDbService> theMovieDbServiceMock = new();
        theMovieDbServiceMock.Setup(m => m.GetCertification("654321")).ReturnsAsync("US:R");

        MovieCreationHelper movieCreationHelper =
            new(moviesDbContextMock.Object, imdbDbContextMock.Object, theMovieDbServiceMock.Object);
        var result = await movieCreationHelper.GetOrCreateMovieByImdbId("654321", true);

        theMovieDbServiceMock.Verify(m => m.GetCertification("654321"));
        theMovieDbServiceMock.VerifyNoOtherCalls();

        Assert.Equal("654321", result.ImdbId);
        Assert.Equal(88, result.ImdbRating);
        Assert.Equal(66666, result.ImdbVotes);
        Assert.Equal("US:R", result.Certification);
        Assert.Null(result.OriginalTitle);
        Assert.False(result.ImdbIgnore);
    }

    [Fact]
    public async Task TestNonExistingWithRefresh2()
    {
        var (moviesDbContextMock, movieEventsDbSetMock) = GetMoviesDbContextMock();
        var (imdbDbContextMock, imdbMoviesDbSetMock) = GetImdbDbContextMock();

        Mock<ITheMovieDbService> theMovieDbServiceMock = new();
        theMovieDbServiceMock.Setup(m => m.GetCertification("654321")).ReturnsAsync((string?)null);

        MovieCreationHelper movieCreationHelper =
            new(moviesDbContextMock.Object, imdbDbContextMock.Object, theMovieDbServiceMock.Object);
        var result = await movieCreationHelper.GetOrCreateMovieByImdbId("654321", true);

        theMovieDbServiceMock.Verify(m => m.GetCertification("654321"));
        theMovieDbServiceMock.VerifyNoOtherCalls();

        Assert.Equal("654321", result.ImdbId);
        Assert.Equal(88, result.ImdbRating);
        Assert.Equal(66666, result.ImdbVotes);
        Assert.Equal("", result.Certification);
        Assert.Null(result.OriginalTitle);
        Assert.False(result.ImdbIgnore);
    }

    [Fact]
    public async Task TestNonExistingWithRefresh3()
    {
        var (moviesDbContextMock, movieEventsDbSetMock) = GetMoviesDbContextMock();
        var (imdbDbContextMock, imdbMoviesDbSetMock) = GetImdbDbContextMock();

        Mock<ITheMovieDbService> theMovieDbServiceMock = new();

        MovieCreationHelper movieCreationHelper =
            new(moviesDbContextMock.Object, imdbDbContextMock.Object, theMovieDbServiceMock.Object);
        var result = await movieCreationHelper.GetOrCreateMovieByImdbId(null!, true);

        theMovieDbServiceMock.VerifyNoOtherCalls();

        Assert.Null(result.ImdbId);
        Assert.Null(result.ImdbRating);
        Assert.Null(result.ImdbVotes);
        Assert.Null(result.Certification);
        Assert.Equal("The hunt for Red October", result.OriginalTitle);
        Assert.False(result.ImdbIgnore);
    }
}