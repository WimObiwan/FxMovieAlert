using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    private readonly Channel _channelVtmGo;
    private readonly Channel _channelVtmGoPlus;
    private readonly Channel _channelVtmGoCinema;
    private readonly Channel _channelStreamz;
    private readonly Channel _channelStreamzPremiumPlus;
    private readonly HttpClient _httpClient;
    private readonly ILogger<VtmGoService2> _logger;

    public VtmGoService2(
        ILogger<VtmGoService2> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("vtmgo");

        _channelVtmGo = new Channel
        {
            Code = "vtmgo",
            Name = "VTM GO",
            LogoS = "https://www.filmoptv.be/images/vtmgo.png"
        };

        _channelVtmGoPlus = new Channel
        {
            Code = "vtmgoplus",
            Name = "VTM GO+",
            LogoS = "https://www.filmoptv.be/images/vtmgo.png"
        };

        _channelVtmGoCinema = new Channel
        {
            Code = "vtmgocinema",
            Name = "VTM GO Cinema",
            LogoS = "https://www.filmoptv.be/images/vtmgo.png"
        };

        _channelStreamz = new Channel
        {
            Code = "streamzbasic",
            Name = "Streamz",
            LogoS = "https://www.filmoptv.be/images/streamzbasic.png"
        };

        _channelStreamzPremiumPlus = new Channel
        {
            Code = "streamzplus",
            Name = "Streamz Premium+",
            LogoS = "https://www.filmoptv.be/images/streamzplus.png"
        };
    }

    public string ProviderName => "VtmGo";

    public string ProviderCode => "vtmgo";

    public IList<string> ChannelCodes => new List<string>() { "vtmgo", "vtmgocinema", "streamzbasic", "streamzplus" };

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
            .SelectMany(e => e.GetElementsByClassName("teaser__link"))
            .OfType<IHtmlAnchorElement>()
            .Where(e => e.GetAttribute("js-module") == "teaser")
            .Select(e => e.Href)
            .Distinct()
            .AsEnumerable();
    }

    private async Task<MovieEvent?> GetMovieDetails(string url)
    {
// #if DEBUG
//         // Quick test
//         url = "https://www.vtmgo.be/vtmgo/top-gun-maverick~c8e7967a-b077-4645-b3db-e7469c2cf6b7";
// #endif

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        var parser = new HtmlParser();
        var document = await parser.ParseDocumentAsync(stream);

        var title = document.GetElementsByClassName("detailv3__title")
            .Select(e => e.Text())
            .FirstOrDefault();

        _logger.LogInformation($"Fetching {title}");

        var broadcasters = document.GetElementsByClassName("detailv3__broadcasters")
            .SelectMany(e => e.GetElementsByTagName("img"))
            .OfType<IHtmlImageElement>()
            .Select(e => e.AlternativeText)
            .OfType<string>()
            .ToList();

        const string vtmGoAltText = "VTM";
        const string vtmGoPlusAltText = "VTM GO+";
        const string streamzAltText = "Streamz Basic";
        const string streamzPlusAltText = "Streamz Premium+";
        const string cinemaAltText = "Cinema";

        _logger.LogInformation($"Using broadcasters {string.Join('/', broadcasters)}");

        var vtmGo = broadcasters.Any(b => b == vtmGoAltText); // VTM...
        var vtmGoPlus = broadcasters.Any(b => b == vtmGoPlusAltText); // VTM...
        var streamz = broadcasters.Any(b => b == streamzAltText); // Streamz...
        var streamzPlus = broadcasters.Any(b => b == streamzPlusAltText); // StreamzPlus...
        var cinema = broadcasters.Any(b => b == cinemaAltText); // VtmGo Cinema...

        #if DEBUG
        var allowedValues = new HashSet<string> {
            vtmGoAltText, vtmGoPlusAltText, streamzAltText, streamzPlusAltText, cinemaAltText,
            "PARAMOUNT_PLUS", "STUDIO_100", "VRT" };
        // Check if any value is not in the allowed set
        Debug.Assert(!broadcasters.Any(b => !allowedValues.Contains(b)));
        #endif

        var labels = document.GetElementsByClassName("detailv3__meta-label")
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

        // 2025-10-12: Doesn't seem to work anymore, when not logged in on the site
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

        var description = 
            document.All
                .OfType<IHtmlMetaElement>()
                .Where(m => m.Name == "description")
                .Select(m => m.Content)
                .FirstOrDefault()
            ??
            document
                .GetElementsByClassName("detailv3__description")
                .OfType<IHtmlParagraphElement>()
                .Select(p => p.TextContent)
                .FirstOrDefault();


        Channel channel;
        MovieEvent.FeedType feedType;
        // if (vtmGo)
        // {
        //     channel = _channelVtmGo;
        //     feedType = MovieEvent.FeedType.FreeVod;
        // }
        // else
        if (vtmGoPlus)
        {
            channel = _channelVtmGoPlus;
            feedType = MovieEvent.FeedType.PaidVod;
        }
        else if (streamz)
        {
            channel = _channelStreamz;
            feedType = MovieEvent.FeedType.PaidVod;
        }
        else if (streamzPlus)
        {
            channel = _channelStreamzPremiumPlus;
            feedType = MovieEvent.FeedType.PaidVod;
        }
        else if (cinema)
        {
            channel = _channelVtmGoCinema;
            feedType = MovieEvent.FeedType.PaidVod;
        }
        else
        {
            channel = _channelVtmGo;
            feedType = MovieEvent.FeedType.FreeVod;
        }

        return new MovieEvent
        {
            ExternalId = url,
            Title = title,
            Year = year,
            Content = description,
            PosterS = image,
            PosterM = image,
            Channel = channel,
            Duration = durationMin,
            Vod = true,
            Feed = feedType,
            VodLink = url,
            Type = 1,
            StartTime = DateTime.MinValue,
            EndTime = DateTime.Now.Date.AddDays((availableDays ?? 300) + 1)
        };
    }
}