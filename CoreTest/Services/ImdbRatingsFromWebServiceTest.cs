using FxMovies.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace FxMovies.CoreTest.Services;

public class ImdbRatingsFromWebServiceTest
{
    [Fact(Skip = "Runtime")]
    //[Fact]
    public async Task GetRatingsAsync_RealData_ReturnsAtLeast4000Entries()
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

        services.AddHttpClient("imdb-graphql", c => 
        { 
            c.DefaultRequestHeaders.Add("accept", "application/json");
            c.DefaultRequestHeaders.Add("accept-language", "en-US,en;q=0.5");
            c.DefaultRequestHeaders.Add("user-agent", "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
            c.DefaultRequestHeaders.Add("x-imdb-client-name", "imdb-web-next-localized");
        });

        var serviceProvider = services.BuildServiceProvider();
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        var logger = NullLogger<ImdbRatingsFromWebService>.Instance;
        var service = new ImdbRatingsFromWebService(httpClientFactory, logger);
        
        // Act
        var result = await service.GetRatingsAsync("ur27490911", null);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Count >= 4000, $"Expected at least 4000 entries, but got {result.Count}");
        
        // Verify that entries have valid data
        foreach (var item in result.Take(10)) // Check first 10 items
        {
            Assert.NotNull(item.ImdbId);
            Assert.NotEmpty(item.ImdbId);
            Assert.True(item.ImdbId.StartsWith("tt"), $"ImdbId should start with 'tt', but got: {item.ImdbId}");
            Assert.True(item.Rating >= 1 && item.Rating <= 10, $"Rating should be between 1-10, but got: {item.Rating}");
            Assert.True(item.Date > DateTime.MinValue, "Date should be set");
        }
    }
}
