using EntityFrameworkCoreMock;
using FxMovies.Core.Entities;
using FxMovies.Core.Queries;
using FxMovies.MoviesDB;

namespace FxMovies.CoreTest;

public class StatsQueryTest
{
    [Fact]
    public async Task Test()
    {
        var data = new[]
        {
            new User
            {
                UserId = "a654321",
                ImdbUserId = "t654321",
                LastUsageTime = new DateTime(2022, 10, 1),
                Usages = 15,
                UserRatings = new List<UserRating>(),
                UserWatchListItems = new List<UserWatchListItem>()
            },
            new User
            {
                UserId = "a654322",
                ImdbUserId = "t654322",
                LastUsageTime = new DateTime(2022, 10, 2),
                Usages = 15,
                UserRatings = new List<UserRating>(),
                UserWatchListItems = new List<UserWatchListItem>()
            },
            new User
            {
                UserId = "a654323",
                ImdbUserId = "t654323",
                LastUsageTime = new DateTime(2022, 10, 3),
                Usages = 15,
                UserRatings = new List<UserRating>(),
                UserWatchListItems = new List<UserWatchListItem>()
            }
        };

        var dbContextMock = new DbContextMock<MoviesDbContext>(Util.DummyMoviesDbOptions);
        var usersDbSetMock = dbContextMock.CreateDbSetMock(x => x.Users, (x, _) => x, data);
       
        StatsQuery statsQuery = new(dbContextMock.Object);
        var result = await statsQuery.Execute();
        Assert.NotNull(result);
        Assert.NotNull(result.Users);
        Assert.Equal(data.Length, result.Users.Count);
        Assert.Equal(data[0].UserId, result.Users[0].UserId);
        Assert.Equal(data[0].ImdbUserId, result.Users[0].ImdbUserId);
        Assert.Equal(data[0].LastUsageTime, result.Users[0].LastUsageTime);
        Assert.Equal(data[0].Usages, result.Users[0].Usages);
        Assert.Equal(data[0].UserRatings.Count, result.Users[0].RatingCount);
        Assert.Equal(data[0].UserWatchListItems.Count, result.Users[0].WatchListItemsCount);
    }
}