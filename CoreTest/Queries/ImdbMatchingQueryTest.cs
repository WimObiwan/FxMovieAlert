using EntityFrameworkCoreMock;
using FxMovies.Core.Entities;
using FxMovies.Core.Queries;
using FxMovies.ImdbDB;
using Microsoft.Extensions.Options;
using Moq;

namespace FxMovies.CoreTest;

public class ImdbMatchingQueryTest
{
    private static readonly ImdbMovie movie1976 = new()
    {
        Id = 1976,
        Year = 1976,
        PrimaryTitle = "Movie Title"
    };

    private static readonly ImdbMovie movie2022 = new()
    {
        Id = 2022,
        Year = 2022,
        PrimaryTitle = "Movie Title 2022"
    };

    private static readonly ImdbMovie movie2050 = new()
    {
        Id = 2050,
        Year = 2050,
        PrimaryTitle = "Movie Title"
    };

    private static readonly ImdbMovieAlternative[] data =
    {
        new()
        {
            Id = 1976 * 10 + 0,
            Movie = movie1976,
            AlternativeTitle = null,
            Normalized = "MOVIE TITLE"
        },
        new()
        {
            Id = 1976 * 10 + 1,
            Movie = movie1976,
            AlternativeTitle = "Movie Title First Alternative 1976",
            Normalized = "MOVIE TITLE FIRST ALTERNATIVE 1976"
        },
        new()
        {
            Id = 1976 * 10 + 2,
            Movie = movie1976,
            AlternativeTitle = "Movie Second First Alternative 1976",
            Normalized = "MOVIE TITLE SECOND ALTERNATIVE 1976"
        },
        new()
        {
            Id = 2022 * 10 + 0,
            Movie = movie2022,
            AlternativeTitle = null,
            Normalized = "MOVIE TITLE 2022"
        },
        new()
        {
            Id = 2022 * 10 + 1,
            Movie = movie2022,
            AlternativeTitle = "Movie Title First Alternative 2022",
            Normalized = "MOVIE TITLE FIRST ALTERNATIVE 2022"
        },
        new()
        {
            Id = 2022 * 10 + 2,
            Movie = movie2022,
            AlternativeTitle = "Movie Second First Alternative 2022",
            Normalized = "MOVIE TITLE SECOND ALTERNATIVE 2022"
        },
        new()
        {
            Id = 2050 * 10 + 0,
            Movie = movie2050,
            AlternativeTitle = "",
            Normalized = "MOVIE TITLE"
        }
    };

    private async Task<ImdbMatchingQueryResult> Run(string movieTitle, int? movieReleaseYear)
    {
        return await Run(movieTitle, movieReleaseYear, 1);
    }

    private async Task<ImdbMatchingQueryResult> Run(string movieTitle, int? movieReleaseYear, int? imdbHuntingYearDiff)
    {
        Mock<IOptionsSnapshot<ImdbMatchingQueryOptions>> optionsMock = new();
        optionsMock.Setup(o => o.Value).Returns(new ImdbMatchingQueryOptions
        {
            ImdbHuntingYearDiff = imdbHuntingYearDiff
        });

        var imdbContextMock = new DbContextMock<ImdbDbContext>(Util.DummyImdbDbOptions);
        var imdbMovieAlternativesDbSetMock =
            imdbContextMock.CreateDbSetMock(x => x.MovieAlternatives, (x, _) => x, data);

        ImdbMatchingQuery imdbMatchingQuery = new(optionsMock.Object, imdbContextMock.Object);
        return await imdbMatchingQuery.Execute(movieTitle, movieReleaseYear);
    }

    [Fact]
    public async Task NotFound()
    {
        var result = await Run("Unknown", 2022);
        Assert.NotNull(result);
        Assert.Null(result.ImdbMovie);
        Assert.Equal(4, result.HuntNo);
    }

    [Fact]
    public async Task NotFoundWithoutYear()
    {
        var result = await Run("Unknown", null);
        Assert.NotNull(result);
        Assert.Null(result.ImdbMovie);
        Assert.Equal(4, result.HuntNo);
    }

    [Fact]
    public async Task FoundWithoutYear()
    {
        var result = await Run("Movie title 2022", null);
        Assert.NotNull(result);
        Assert.NotNull(result.ImdbMovie);
        Assert.Equal(2022, result.ImdbMovie!.Id);
        Assert.Equal("Movie Title 2022", result.ImdbMovie.PrimaryTitle);
        Assert.Equal(0, result.HuntNo);
    }

    [Fact]
    public async Task FoundWithExactYear()
    {
        var result = await Run("Movie title 2022", 2022);
        Assert.NotNull(result);
        Assert.NotNull(result.ImdbMovie);
        Assert.Equal(2022, result.ImdbMovie!.Id);
        Assert.Equal("Movie Title 2022", result.ImdbMovie.PrimaryTitle);
        Assert.Equal(0, result.HuntNo);
    }

    [Fact]
    public async Task FoundWithApproximateYear1()
    {
        var result = await Run("Movie title 2022", 2021);
        Assert.NotNull(result);
        Assert.NotNull(result.ImdbMovie);
        Assert.Equal(2022, result.ImdbMovie!.Id);
        Assert.Equal("Movie Title 2022", result.ImdbMovie.PrimaryTitle);
        Assert.Equal(2, result.HuntNo);
    }

    [Fact]
    public async Task FoundWithApproximateYear2()
    {
        var result = await Run("Movie title 2022", 2023);
        Assert.NotNull(result);
        Assert.NotNull(result.ImdbMovie);
        Assert.Equal(2022, result.ImdbMovie!.Id);
        Assert.Equal("Movie Title 2022", result.ImdbMovie.PrimaryTitle);
        Assert.Equal(2, result.HuntNo);
    }

    [Fact]
    public async Task NotFoundWithWrongYear1()
    {
        var result = await Run("Movie title 2022", 2020);
        Assert.NotNull(result);
        Assert.Null(result.ImdbMovie);
    }

    [Fact]
    public async Task NotFoundWithWrongYear2()
    {
        var result = await Run("Movie title 2022", 2024);
        Assert.NotNull(result);
        Assert.Null(result.ImdbMovie);
    }

    [Fact]
    public async Task FoundWithWrongYear1AndDefaultHuntingError()
    {
        var result = await Run("Movie title 2022", 2020, 2);
        Assert.NotNull(result);
        Assert.NotNull(result.ImdbMovie);
        Assert.Equal(2022, result.ImdbMovie!.Id);
        Assert.Equal("Movie Title 2022", result.ImdbMovie.PrimaryTitle);
        Assert.Equal(2, result.HuntNo);
    }

    [Fact]
    public async Task FoundWithWrongYear2AndDefaultHuntingError()
    {
        var result = await Run("Movie title 2022", 2024, 2);
        Assert.NotNull(result);
        Assert.NotNull(result.ImdbMovie);
        Assert.Equal(2022, result.ImdbMovie!.Id);
        Assert.Equal("Movie Title 2022", result.ImdbMovie.PrimaryTitle);
        Assert.Equal(2, result.HuntNo);
    }

    [Fact]
    public async Task FoundOneOfDoubleWithoutYear()
    {
        var result = await Run("Movie title", null);
        Assert.NotNull(result);
        Assert.NotNull(result.ImdbMovie);
        Assert.True(1976 == result.ImdbMovie!.Id || 2050 == result.ImdbMovie!.Id);
        Assert.Equal("Movie Title", result.ImdbMovie.PrimaryTitle);
    }

    [Fact]
    public async Task FoundFirstOfDoubleWithExactYear()
    {
        var result = await Run("Movie title", 1976);
        Assert.NotNull(result);
        Assert.NotNull(result.ImdbMovie);
        Assert.Equal(1976, result.ImdbMovie!.Id);
        Assert.Equal("Movie Title", result.ImdbMovie.PrimaryTitle);
        Assert.Equal(0, result.HuntNo);
    }

    [Fact]
    public async Task FoundSecondOfDoubleWithExactYear()
    {
        var result = await Run("Movie title", 2050);
        Assert.NotNull(result);
        Assert.NotNull(result.ImdbMovie);
        Assert.Equal(2050, result.ImdbMovie!.Id);
        Assert.Equal("Movie Title", result.ImdbMovie.PrimaryTitle);
        Assert.Equal(1, result.HuntNo);
    }

    [Fact]
    public async Task FoundFirstOfDoubleWithApproximateYear()
    {
        var result = await Run("Movie title", 1975);
        Assert.NotNull(result);
        Assert.NotNull(result.ImdbMovie);
        Assert.Equal(1976, result.ImdbMovie!.Id);
        Assert.Equal("Movie Title", result.ImdbMovie.PrimaryTitle);
        Assert.Equal(2, result.HuntNo);
    }

    [Fact]
    public async Task FoundSecondOfDoubleWithApproximateYear()
    {
        var result = await Run("Movie title", 2051);
        Assert.NotNull(result);
        Assert.NotNull(result.ImdbMovie);
        Assert.Equal(2050, result.ImdbMovie!.Id);
        Assert.Equal("Movie Title", result.ImdbMovie.PrimaryTitle);
        Assert.Equal(3, result.HuntNo);
    }

    [Fact]
    public async Task NotFoundFirstOfDoubleWithWrongYear()
    {
        var result = await Run("Movie title", 1974);
        Assert.NotNull(result);
        Assert.Null(result.ImdbMovie);
    }

    [Fact]
    public async Task NotFoundSecondOfDoubleWithWrongYear()
    {
        var result = await Run("Movie title", 2052);
        Assert.NotNull(result);
        Assert.Null(result.ImdbMovie);
    }

    [Fact]
    public async Task FoundFirstOfDoubleWithApproximateYearAndDefaultHuntingError()
    {
        var result = await Run("Movie title", 1974, null);
        Assert.NotNull(result);
        Assert.NotNull(result.ImdbMovie);
        Assert.Equal(1976, result.ImdbMovie!.Id);
        Assert.Equal("Movie Title", result.ImdbMovie.PrimaryTitle);
        Assert.Equal(2, result.HuntNo);
    }

    [Fact]
    public async Task FoundSecondOfDoubleWithApproximateYearAndDefaultHuntingError()
    {
        var result = await Run("Movie title", 2052, null);
        Assert.NotNull(result);
        Assert.NotNull(result.ImdbMovie);
        Assert.Equal(2050, result.ImdbMovie!.Id);
        Assert.Equal("Movie Title", result.ImdbMovie.PrimaryTitle);
        Assert.Equal(3, result.HuntNo);
    }
}