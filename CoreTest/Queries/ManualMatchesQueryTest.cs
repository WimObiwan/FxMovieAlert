using EntityFrameworkCoreMock;
using FxMovies.Core.Entities;
using FxMovies.Core.Queries;
using FxMovies.MoviesDB;

namespace FxMovies.CoreTest;

public class ManualMatchesQueryTest
{
    [Fact]
    public async Task Test()
    {
        var data = new[]
        {
            new ManualMatch
            {
                Id = 1,
                AddedDateTime = new DateTime(2022, 10, 1),
                Title = "Test 1",
                NormalizedTitle = "TEST 1",
                Movie = null
            },
            new ManualMatch
            {
                Id = 2,
                AddedDateTime = new DateTime(2022, 10, 2),
                Title = "Test 2",
                NormalizedTitle = "TEST 2",
                Movie = null
            },
            new ManualMatch
            {
                Id = 3,
                AddedDateTime = new DateTime(2022, 10, 3),
                Title = "Test 3",
                NormalizedTitle = "TEST 3",
                Movie = null
            }
        };

        var dbContextMock = new DbContextMock<MoviesDbContext>(Util.DummyMoviesDbOptions);
        var manualMatchesDbSetMock = dbContextMock.CreateDbSetMock(x => x.ManualMatches, (x, _) => x, data);
       
        string movieTitle = "Unknown";

        ManualMatchesQuery manualMatchesQuery = new(dbContextMock.Object);
        var result = await manualMatchesQuery.Execute(movieTitle);
        Assert.Null(result);

        movieTitle = "Test 2";

        result = await manualMatchesQuery.Execute(movieTitle);
        Assert.NotNull(result);
        if (result != null)
        {
            Assert.Equal(data[1].Id, result.Id);
            Assert.Equal(data[1].AddedDateTime, result.AddedDateTime);
            Assert.Equal(data[1].Title, result.Title);
            Assert.Equal(data[1].NormalizedTitle, result.NormalizedTitle);
            Assert.Equal(data[1].Movie, result.Movie);
        }
    }
}