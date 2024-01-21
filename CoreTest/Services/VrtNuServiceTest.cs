using System.Net;
using FxMovies.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Contrib.HttpClient;

namespace FxMovies.CoreTest;

public class VrtNuServiceTest
{
    [Fact]
    public async Task Test()
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);

        handler.SetupRequest(HttpMethod.Get, "https://www.vrt.be/vrtnu/a-z/")
            .ReturnsResponse(HttpStatusCode.OK, File.ReadAllText("Assets/vrtnu-all.html.txt"), "text/html");

        handler.SetupRequest(HttpMethod.Get,
                r => r.RequestUri != null && r.RequestUri.AbsoluteUri.StartsWith("https://www.vrt.be/vrtnu/a-z/") &&
                     r.RequestUri.AbsoluteUri.EndsWith(".json"))
            .ReturnsResponse(HttpStatusCode.OK, File.ReadAllText("Assets/vrtnu-detail.json.txt"), "text/html");

        var factory = handler.CreateClientFactory();

        Mock.Get(factory).Setup(x => x.CreateClient("vrtnu"))
            .Returns(() =>
            {
                var client = handler.CreateClient();
                client.BaseAddress = new Uri("https://www.vrt.be/vrtnu/a-z/");
                return client;
            });

        VrtNuService vrtNuService = new(factory);
        vrtNuService.MaxCount = 1;
        var result = await vrtNuService.GetMovieEvents();

        Assert.NotNull(result);
        Assert.Single(result);
        foreach (var movieEvent in result)
        {
            Assert.Equal("8eraf", movieEvent.Title);
            Assert.Equal(83, movieEvent.Duration);
        }
    }

    [Fact(Skip = "Runtime")]
    public async Task RealTest()
    {
        IServiceCollection services = new ServiceCollection(); // [1]
        services.AddHttpClient("vrtnu", c => { c.BaseAddress = new Uri("https://www.vrt.be/vrtnu/a-z/"); });

        VrtNuService vrtNuService = new(
            services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>());
        var result = await vrtNuService.GetMovieEvents();

        Assert.NotNull(result);
        foreach (var movieEvent in result) Assert.NotNull(movieEvent.Title);
        //Assert.NotNull(movieEvent.Duration);
    }
}