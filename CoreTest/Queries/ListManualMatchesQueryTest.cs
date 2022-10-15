using EntityFrameworkCoreMock;
using FxMovies.Core.Entities;
using FxMovies.Core.Queries;
using FxMovies.MoviesDB;

namespace FxMovies.CoreTest;

public class ListManualMatchesQueryTest
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

        ListManualMatchesQuery listManualMatchesQuery = new(dbContextMock.Object);
        var result = await listManualMatchesQuery.Execute();
        Assert.NotNull(result);
        Assert.Equal(data.Length, result.Count);
        Assert.Equal(data[0].Id, result[0].Id);
        Assert.Equal(data[0].AddedDateTime, result[0].AddedDateTime);
        Assert.Equal(data[0].Title, result[0].Title);
        Assert.Equal(data[0].NormalizedTitle, result[0].NormalizedTitle);
        Assert.Equal(data[0].Movie, result[0].Movie);
    }
}