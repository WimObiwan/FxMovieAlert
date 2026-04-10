using FxMovies.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FxMovies.CoreTest;

public class VtmGoServiceTest
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

    //[Fact]
    [ForceRunFact("VTMGO")]
    public async Task RealTest()
    {
        IServiceCollection services = new ServiceCollection(); // [1]
        services.AddLogging();
        services.AddHttpClient("vtmgo", c =>
        {
            c.BaseAddress = new Uri("https://www.vtmgo.be/vtmgo/");
        }).ConfigurePrimaryHttpMessageHandler(() =>
        {
            var handler = new HttpClientHandler();
            handler.CookieContainer.Add(new Uri("https://www.vtmgo.be"), new System.Net.Cookie("authId", "00000000-0000-0000-0000-000000000001"));
            return handler;
        });
        services.AddHttpClient("vtmgo_login", c =>
        {
            c.BaseAddress = new Uri("https://login2.vtm.be");
            c.DefaultRequestHeaders.Add("User-Agent",
                "VTMGO/10.3 (be.vmma.vtm.zenderapp; build:13259; Android 25) okhttp/4.9.0");
            c.DefaultRequestHeaders.Add("x-app-version", "10");
            c.DefaultRequestHeaders.Add("x-persgroep-mobile-app", "true");
            c.DefaultRequestHeaders.Add("x-persgroep-os", "android");
            c.DefaultRequestHeaders.Add("x-persgroep-os-version", "25");
        });
        services.AddHttpClient("vtmgo_dpg", c =>
        {
            c.BaseAddress = new Uri("https://lfvp-api.dpgmedia.net");
            c.DefaultRequestHeaders.Add("User-Agent",
                "VTMGO/10.3 (be.vmma.vtm.zenderapp; build:13259; Android 25) okhttp/4.9.0");
            c.DefaultRequestHeaders.Add("x-app-version", "10");
            c.DefaultRequestHeaders.Add("x-persgroep-mobile-app", "true");
            c.DefaultRequestHeaders.Add("x-persgroep-os", "android");
            c.DefaultRequestHeaders.Add("x-persgroep-os-version", "25");
        });

        var serviceProvider = services.BuildServiceProvider();

        VtmGoService vtmGoService = new(
            serviceProvider.GetRequiredService<ILogger<VtmGoService>>(),
            serviceProvider.GetRequiredService<IHttpClientFactory>());
        var result = await vtmGoService.GetMovieEvents();

        Assert.NotNull(result);
        Assert.NotEmpty(result);
        var today = DateTime.Now.Date;
        Assert.True(result.Any(me => me.EndTime > today && me.EndTime < today.AddHours(36)), "Expected at least one movie event that ends within the next 36 hours.");
        Assert.True(result.Any(me => me.EndTime > today.AddHours(36) && me.EndTime < today.AddDays(30)), "Expected at least one movie event that ends within one month.");
        Assert.True(result.Any(me => me.EndTime >= today.AddDays(30)), "Expected at least one movie event that ends later then 1 month.");
        foreach (var movieEvent in result)
        {
            Assert.NotNull(movieEvent.Title);
            Assert.NotNull(movieEvent.Duration);
        }
    }
}