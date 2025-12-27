using FxMovies.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FxMovies.CoreTest.Services;

public class ImdbWatchlistFromWebServiceTest
{
    [Fact(Skip = "Runtime")]
    // [Fact]
    public async Task GetWatchlistAsync_RealData_ReturnsAtLeast1000Entries()
    {
        // Arrange
        IServiceCollection services = new ServiceCollection();
        services.AddHttpClient("imdb-web", c => 
        { 
            c.BaseAddress = new Uri("https://www.imdb.com");
            c.DefaultRequestHeaders.Add("accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            c.DefaultRequestHeaders.Add("accept-language", "en-US,en;q=0.5");
            c.DefaultRequestHeaders.Add("user-agent", "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
        });

        var serviceProvider = services.BuildServiceProvider();
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        var service = new ImdbWatchlistFromWebService(httpClientFactory);
        
        // Act
        var result = await service.GetWatchlistAsync("ur27490911");

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Count >= 1000, $"Expected at least 1000 entries, but got {result.Count}");
        
        // Verify that entries have valid data
        foreach (var item in result.Take(10)) // Check first 10 items
        {
            Assert.NotNull(item.ImdbId);
            Assert.NotEmpty(item.ImdbId);
            Assert.True(item.ImdbId.StartsWith("tt"), $"ImdbId should start with 'tt', but got: {item.ImdbId}");
        }
    }
}
