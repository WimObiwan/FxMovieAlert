using FxMovies.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FxMovies.CoreTest;

public class VrtNuServiceTest
{
    // [Fact]
    // public async Task Test()
    // {
    //     var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);

    //     handler.SetupRequest(HttpMethod.Get, "https://www.vrt.be/vrtnu/a-z/")
    //         .ReturnsResponse(HttpStatusCode.OK, File.ReadAllText("Assets/vrtnu-all.html.txt"), "text/html");

    //     handler.SetupRequest(HttpMethod.Get,
    //             r => r.RequestUri != null && r.RequestUri.AbsoluteUri.StartsWith("https://www.vrt.be/vrtnu/a-z/") &&
    //                  r.RequestUri.AbsoluteUri.EndsWith(".json"))
    //         .ReturnsResponse(HttpStatusCode.OK, File.ReadAllText("Assets/vrtnu-detail.json.txt"), "text/html");

    //     var factory = handler.CreateClientFactory();

    //     Mock.Get(factory).Setup(x => x.CreateClient("vrtnu"))
    //         .Returns(() =>
    //         {
    //             var client = handler.CreateClient();
    //             client.BaseAddress = new Uri("https://www.vrt.be/vrtnu/a-z/");
    //             return client;
    //         });

    //     VrtMaxService vrtMaxService = new(factory);
    //     vrtMaxService.MaxCount = 1;
    //     var result = await vrtMaxService.GetMovieEvents();

    //     Assert.NotNull(result);
    //     Assert.Single(result);
    //     foreach (var movieEvent in result)
    //     {
    //         Assert.Equal("8eraf", movieEvent.Title);
    //         Assert.Equal(83, movieEvent.Duration);
    //     }
    // }

    // [Fact]
    [Fact(Skip = "Runtime")]
    public async Task RealTest()
    {
        IServiceCollection services = new ServiceCollection(); // [1]
        services.AddHttpClient("vrtmax", 
            c => 
            {
                c.BaseAddress = new Uri("https://www.vrt.be/");

                // Set headers
                c.DefaultRequestHeaders.Add("accept", "application/graphql-response+json, application/graphql+json, application/json, text/event-stream, multipart/mixed");
                c.DefaultRequestHeaders.Add("accept-language", "nl");
                c.DefaultRequestHeaders.Add("cache-control", "no-cache");
                c.DefaultRequestHeaders.Add("dnt", "1");
                c.DefaultRequestHeaders.Add("origin", "https://www.vrt.be");
                c.DefaultRequestHeaders.Add("pragma", "no-cache");
                c.DefaultRequestHeaders.Add("referer", "https://www.vrt.be/vrtmax/films/");
                c.DefaultRequestHeaders.Add("sec-ch-ua", "\"Chromium\";v=\"128\", \"Not;A=Brand\";v=\"24\", \"Microsoft Edge\";v=\"128\"");
                c.DefaultRequestHeaders.Add("sec-ch-ua-mobile", "?1");
                c.DefaultRequestHeaders.Add("sec-ch-ua-platform", "\"Android\"");
                c.DefaultRequestHeaders.Add("sec-fetch-dest", "empty");
                c.DefaultRequestHeaders.Add("sec-fetch-mode", "cors");
                c.DefaultRequestHeaders.Add("sec-fetch-site", "same-origin");
                c.DefaultRequestHeaders.Add("user-agent", "Mozilla/5.0 (Linux; Android 8.0.0; SM-G955U Build/R16NW) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/116.0.0.0 Mobile Safari/537.36 Edg/128.0.0.0");
                c.DefaultRequestHeaders.Add("x-vrt-client-name", "WEB");
                c.DefaultRequestHeaders.Add("x-vrt-client-version", "1.5.14");
                c.DefaultRequestHeaders.Add("x-vrt-zone", "default");
            });


        VrtMaxService vrtMaxService = new(
            services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>());
        var result = await vrtMaxService.GetMovieEvents();

        Assert.NotNull(result);
        foreach (var movieEvent in result)
        {
            Assert.NotNull(movieEvent.Title);
            Assert.NotNull(movieEvent.Duration);
        }
    }
}