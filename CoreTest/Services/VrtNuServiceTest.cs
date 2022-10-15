using System;
using System.Net;
using System.Net.Http;
using EntityFrameworkCoreMock;
using FxMovies.Core.Entities;
using FxMovies.Core.Queries;
using FxMovies.Core.Services;
using FxMovies.MoviesDB;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Contrib.HttpClient;
using Xunit;

namespace FxMovies.CoreTest;

public class VrtNuServiceTest
{
    [Fact]
    public async Task Test()
    {
        var loggerMock = new Mock<ILogger<VrtNuService>>();
        
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);

        handler.SetupRequest(HttpMethod.Get, "https://www.vrt.be/vrtnu/a-z/")
            .ReturnsResponse(HttpStatusCode.OK, File.ReadAllText("Assets/vrtnu-all.html.txt"), "text/html");

        handler.SetupRequest(HttpMethod.Get,
            r => r.RequestUri != null && r.RequestUri.AbsoluteUri.StartsWith("https://www.vrt.be/vrtnu/a-z/") && r.RequestUri.AbsoluteUri.EndsWith(".json"))
            .ReturnsResponse(HttpStatusCode.OK, File.ReadAllText("Assets/vrtnu-detail.json.txt"), "text/html");

        var factory = handler.CreateClientFactory();

        Mock.Get(factory).Setup(x => x.CreateClient("vrtnu"))
            .Returns(() =>
            {
                var client = handler.CreateClient();
                client.BaseAddress = new Uri("https://www.vrt.be/vrtnu/a-z/");
                return client;
            });

        var client = factory.CreateClient("vrtnu");
        //client.G
        VrtNuService vrtNuService = new(loggerMock.Object, factory);
        var result = await vrtNuService.GetMovieEvents();

        Assert.NotNull(result);
        Assert.Equal(23, result.Count);
        foreach (var movieEvent in result)
        {
            Assert.Equal("8eraf", movieEvent.Title);
            Assert.Equal(83, movieEvent.Duration);
        }
    }
}