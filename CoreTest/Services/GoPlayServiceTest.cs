using FxMovies.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FxMovies.CoreTest;

public class GoPlayServiceTest
{
    //[Fact]
    [ForceRunFact("GOPLAY")]
    public async Task RealTest()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpClient("goplay", c => { c.BaseAddress = new Uri("https://api.play.tv"); });

        var serviceProvider = services.BuildServiceProvider();

        GoPlayService goPlayService = new(
            serviceProvider.GetRequiredService<ILogger<GoPlayService>>(),
            serviceProvider.GetRequiredService<IHttpClientFactory>());
        var result = await goPlayService.GetMovieEvents();

        Assert.NotNull(result);
        Assert.NotEmpty(result);
        foreach (var movieEvent in result)
        {
            Assert.NotNull(movieEvent.Title);
            Assert.NotNull(movieEvent.Duration);
            Assert.NotNull(movieEvent.VodLink);
            Assert.StartsWith("https://www.play.tv/", movieEvent.VodLink);
        }
    }
}
