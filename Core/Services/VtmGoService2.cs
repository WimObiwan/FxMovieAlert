using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using FxMovies.Core.Entities;
using Microsoft.Extensions.Logging;

namespace FxMovies.Core.Services;

public class VtmGoService2 : IMovieEventService
{
    private readonly Channel _channel;
    private readonly HttpClient _httpClient;
    private readonly ILogger<VtmGoService2> _logger;

    public VtmGoService2(
        ILogger<VtmGoService2> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("vtmgo");

        _channel = new Channel
        {
            Code = "vtmgo",
            Name = "VTM GO",
            LogoS = "https://www.filmoptv.be/images/vtmgo.png"
        };
    }

    public string ProviderName => "VtmGo";

    public string ChannelCode => "vtmgo";

    public async Task<IList<MovieEvent>> GetMovieEvents()
    {
        return (await GetMovieUrls())
            .Select(async url => await GetMovieDetails(url))
            .Select(t => t.Result)
            .Where(me => me != null && me is { Duration: >= 75 })
            .Select(me => me!)
            .ToList();
    }

    private async Task<IEnumerable<string>> GetMovieUrls()
    {
        var response = await _httpClient.GetAsync("films");
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        var parser = new HtmlParser();
        var document = await parser.ParseDocumentAsync(stream);
        return document
            .GetElementsByTagName("x-swimlane__scroller")
            .SelectMany(e => e.GetElementsByClassName("teaser"))
            .OfType<IHtmlAnchorElement>()
            .Select(e => e.Href)
            .Distinct()
            .AsEnumerable();
    }

    private async Task<MovieEvent?> GetMovieDetails(string url)
    {
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        var parser = new HtmlParser();
        var document = await parser.ParseDocumentAsync(stream);

        var title = document.GetElementsByClassName("detail__title")
            .Select(e => e.Text())
            .FirstOrDefault();

        _logger.LogInformation($"Fetching {title}");

        var labels = document.GetElementsByClassName("detail__meta-label")
            .Select(e => e.Text().Trim())
            .ToList();

        _logger.LogInformation($"Using labels {string.Join('/', labels.Select(l => Regex.Replace(l, @"\s+", " ")))}");

        var year = labels
            .Select(l => Regex.Match(l, @"^(\d{4})$"))
            .Where(m => m.Success)
            .Select(m => (int?)int.Parse(m.Groups[1].Value))
            .FirstOrDefault();

        var durationMin = labels
            .Select(l => Regex.Match(l, @"^(\d{1,3}) min$"))
            .Where(m => m.Success)
            .Select(m => (int?)int.Parse(m.Groups[1].Value))
            .FirstOrDefault();

        var availableDays = labels
            .Select(l => Regex.Match(l, @"^Nog (\d{1,3}) dag(?:en)? beschikbaar$"))
            .Where(m => m.Success)
            .Select(m => (int?)int.Parse(m.Groups[1].Value))
            .FirstOrDefault();

        var availableFrom = labels
            .Select(l => Regex.Match(l, @"^Beschikbaar vanaf (.*)$"))
            .Where(m => m.Success)
            .Select(m => m.Groups[1].Value)
            .FirstOrDefault();

        if (availableFrom != null)
            // Not yet available --> ignore
            return null;

        if (!availableDays.HasValue && labels.Any(l => l == "Tot middernacht beschikbaar"))
            availableDays = 0;

        // DateTime? dateTime = labels
        //     .Select(l => Regex.Match(l, @"^\w+ (\d{1,2} \w{2,4}\. \d{4})$"))
        //     .Where(m => m.Success)
        //     .Select(m => (DateTime?)DateTime.Parse(m.Groups[1].Value.Replace(".", ""), CultureInfo.GetCultureInfo("nl-BE")))
        //     .FirstOrDefault();

        var image = document.All
            .OfType<IHtmlMetaElement>()
            .Where(m => m.GetAttribute("property") == "og:image")
            .Select(m => m.Content)
            .FirstOrDefault();

        var description = document.All
            .OfType<IHtmlMetaElement>()
            .Where(m => m.Name == "description")
            .Select(m => m.Content)
            .FirstOrDefault();

        return new MovieEvent
        {
            ExternalId = url,
            Title = title,
            Year = year,
            Content = description,
            PosterS = image,
            PosterM = image,
            Channel = _channel,
            Duration = durationMin,
            Vod = true,
            Feed = MovieEvent.FeedType.FreeVod,
            VodLink = url,
            Type = 1,
            StartTime = DateTime.MinValue,
            EndTime = DateTime.Now.Date.AddDays((availableDays ?? 300) + 1)
        };
    }
}