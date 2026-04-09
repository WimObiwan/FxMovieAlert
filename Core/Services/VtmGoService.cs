using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FxMovies.Core.Entities;
using Microsoft.Extensions.Logging;

namespace FxMovies.Core.Services;

public class VtmGoService : IMovieEventService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Channel _channelVtmGo;
    private readonly Channel _channelVtmGoPlus;
    private readonly Channel _channelVtmGoCinema;
    private readonly Channel _channelStreamz;
    private readonly Channel _channelStreamzPremiumPlus;
    private readonly HttpClient _httpClient;
    private readonly ILogger<VtmGoService> _logger;

    public VtmGoService(
        ILogger<VtmGoService> logger,
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

    private static JsonElement? ExtractNextData(string html)
    {
        var match = Regex.Match(html, @"<script\s+id=""__NEXT_DATA__""[^>]*>(.*?)</script>", RegexOptions.Singleline);
        if (!match.Success)
            return null;
        return JsonSerializer.Deserialize<JsonElement>(match.Groups[1].Value);
    }

    private async Task<IEnumerable<string>> GetMovieUrls()
    {
        var response = await _httpClient.GetAsync("storefront/films");
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();
        var nextData = ExtractNextData(html);
        if (nextData == null)
        {
            _logger.LogWarning("Could not extract __NEXT_DATA__ from storefront page");
            return Enumerable.Empty<string>();
        }

        var urls = new HashSet<string>();
        var rows = nextData.Value
            .GetProperty("props")
            .GetProperty("pageProps")
            .GetProperty("storefrontData")
            .GetProperty("rows");

        foreach (var row in rows.EnumerateArray())
        {
            if (!row.TryGetProperty("teasers", out var teasers))
                continue;

            foreach (var teaser in teasers.EnumerateArray())
            {
                if (teaser.TryGetProperty("url", out var urlProp))
                {
                    var url = urlProp.GetString();
                    if (!string.IsNullOrEmpty(url))
                        urls.Add(url);
                }
            }
        }

        _logger.LogInformation("Found {Count} unique movie URLs from storefront", urls.Count);
        return urls;
    }

    private async Task<MovieEvent?> GetMovieDetails(string relativeUrl)
    {
        var response = await _httpClient.GetAsync(relativeUrl.TrimStart('/'));
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();
        var nextData = ExtractNextData(html);
        if (nextData == null)
        {
            _logger.LogWarning("Could not extract __NEXT_DATA__ from detail page {Url}", relativeUrl);
            return null;
        }

        var detailData = nextData.Value
            .GetProperty("props")
            .GetProperty("pageProps")
            .GetProperty("detailData");

        // Only process movies
        if (detailData.TryGetProperty("type", out var typeProp) && typeProp.GetString() != "MOVIE")
            return null;

        var title = detailData.GetProperty("title").GetProperty("label").GetString();
        _logger.LogInformation("Fetching {Title}", title);

        // Extract year and duration from headerLabels
        int? year = null;
        int? durationMin = null;
        if (detailData.TryGetProperty("headerLabels", out var headerLabels))
        {
            foreach (var label in headerLabels.EnumerateArray())
            {
                var accessibilityLabel = label.GetProperty("accessibilityLabel").GetString();
                var labelText = label.GetProperty("label").GetString() ?? "";

                if (accessibilityLabel == "Productiejaar")
                {
                    var match = Regex.Match(labelText, @"^(\d{4})$");
                    if (match.Success)
                        year = int.Parse(match.Groups[1].Value);
                }
                else if (accessibilityLabel == "Tijdsduur")
                {
                    var match = Regex.Match(labelText, @"^(\d{1,3}) min$");
                    if (match.Success)
                        durationMin = int.Parse(match.Groups[1].Value);
                }
            }
        }

        // Fallback: use durationSeconds if available
        if (!durationMin.HasValue && detailData.TryGetProperty("durationSeconds", out var durationSec)
            && durationSec.ValueKind == JsonValueKind.Number)
        {
            durationMin = durationSec.GetInt32() / 60;
        }

        var description = detailData.TryGetProperty("description", out var descProp)
            ? descProp.GetString()
            : null;

        // Get image from share or backgroundImageUrl
        string? image = null;
        if (detailData.TryGetProperty("share", out var share)
            && share.TryGetProperty("imageUrl", out var shareImage))
        {
            image = shareImage.GetString();
        }
        else if (detailData.TryGetProperty("backgroundImageUrl", out var bgImage))
        {
            image = bgImage.GetString();
        }

        // Determine channel from broadcasterLogo and premiumProductLogo
        string? broadcasterLabel = null;
        if (detailData.TryGetProperty("broadcasterLogo", out var broadcasterLogo)
            && broadcasterLogo.ValueKind == JsonValueKind.Object)
        {
            broadcasterLabel = broadcasterLogo.GetProperty("accessibilityLabel").GetString();
        }

        string? premiumLabel = null;
        if (detailData.TryGetProperty("premiumProductLogo", out var premiumLogo)
            && premiumLogo.ValueKind == JsonValueKind.Object)
        {
            premiumLabel = premiumLogo.GetProperty("accessibilityLabel").GetString();
        }

        _logger.LogInformation("Broadcaster: {Broadcaster}, Premium: {Premium}", broadcasterLabel, premiumLabel);

        Channel channel;
        MovieEvent.FeedType feedType;

        if (premiumLabel == "Cinema")
        {
            channel = _channelVtmGoCinema;
            feedType = MovieEvent.FeedType.PaidVod;
        }
        else if (premiumLabel == "VTM GO+" || broadcasterLabel == "VTM GO+")
        {
            channel = _channelVtmGoPlus;
            feedType = MovieEvent.FeedType.PaidVod;
        }
        else if (premiumLabel == "Streamz Basic" || broadcasterLabel == "Streamz Basic")
        {
            channel = _channelStreamz;
            feedType = MovieEvent.FeedType.PaidVod;
        }
        else if (premiumLabel == "Streamz Premium+" || broadcasterLabel == "Streamz Premium+")
        {
            channel = _channelStreamzPremiumPlus;
            feedType = MovieEvent.FeedType.PaidVod;
        }
        else
        {
            channel = _channelVtmGo;
            feedType = MovieEvent.FeedType.FreeVod;
        }

        // Check availability from playableUntilLabel
        int? availableDays = null;
        if (detailData.TryGetProperty("playableUntilLabel", out var playableUntil)
            && playableUntil.ValueKind == JsonValueKind.String)
        {
            var playableText = playableUntil.GetString() ?? "";
            var daysMatch = Regex.Match(playableText, @"Nog (\d{1,3}) dag(?:en)? beschikbaar");
            if (daysMatch.Success)
                availableDays = int.Parse(daysMatch.Groups[1].Value);
        }

        // Check for "coming soon" in badges
        if (detailData.TryGetProperty("badges", out var badges))
        {
            foreach (var badge in badges.EnumerateArray())
            {
                var badgeLabel = badge.GetProperty("label").GetString() ?? "";
                if (badgeLabel.Contains("Binnenkort", StringComparison.OrdinalIgnoreCase))
                    return null;
            }
        }

        var fullUrl = $"https://www.vtmgo.be/vtmgo{relativeUrl}";

        return new MovieEvent
        {
            ExternalId = fullUrl,
            Title = title,
            Year = year,
            Content = description,
            PosterS = image,
            PosterM = image,
            Channel = channel,
            Duration = durationMin,
            Vod = true,
            Feed = feedType,
            VodLink = fullUrl,
            Type = 1,
            StartTime = DateTime.MinValue,
            EndTime = DateTime.Now.Date.AddDays((availableDays ?? 300) + 1)
        };
    }
}