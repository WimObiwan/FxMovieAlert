using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AngleSharp.Html.Parser;
using AngleSharp.Html.Dom;
using FxMovies.Core.Entities;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Extensions.Options;

namespace FxMovies.Core.Services;

public class PrimeVideoServiceOptions
{
    public static string Position => "PrimeVideoService";

    public string LocalDownloadOverride { get; set; }
}

public class PrimeVideoService : IMovieEventService
{
    private readonly ILogger<PrimeVideoService> logger;
    private readonly PrimeVideoServiceOptions primeVideoServiceOptions;
    private readonly HttpClient httpClient;

    public PrimeVideoService(
        ILogger<PrimeVideoService> logger,
        IOptions<PrimeVideoServiceOptions> primeVideoServiceOptions,
        IHttpClientFactory httpClientFactory)
    {
        this.logger = logger;
        this.primeVideoServiceOptions = primeVideoServiceOptions.Value;
        this.httpClient = httpClientFactory.CreateClient("primevideo");
    }

    public string ProviderName => "PrimeVideo";

    public string ChannelCode => "primevideo";

    public async Task<IList<MovieEvent>> GetMovieEvents()
    {
        // https://www.primevideo.com/storefront/ref=atv_nb_lcl_nl_BE

        Channel channel = new Channel()
        {
            Code = "primevideo",
            Name = "Prime Video",
            LogoS = "https://www.filmoptv.be/images/primevideo.png"
        };

        var movies = await GetMovieInfo();
        DateTime dateTime;
        if (primeVideoServiceOptions.LocalDownloadOverride == null)
            dateTime = DateTime.Now;
        else
            dateTime = new FileInfo(primeVideoServiceOptions.LocalDownloadOverride).LastWriteTime;

        List<MovieEvent> movieEvents = new List<MovieEvent>();
        foreach (var movie in movies)
        {
            int? year;
            if (int.TryParse(movie.releaseYear, out int year2))
                year = year2;
            else
                year = null;

            int? duration;
            var match = Regex.Match(movie.runtime, @"^(?:(\d+) (?:uur|h) )?(\d+) min$");
            if (match.Success)
            {
                int duration2 = 0;
                if (match.Groups[1].Success)
                {
                    duration2 = int.Parse(match.Groups[1].Value) * 60;
                }
                duration2 += int.Parse(match.Groups[2].Value);
                duration = duration2;
            }
            else
            {
                duration = null;
            }

            movieEvents.Add(new MovieEvent
            {
                Channel = channel,
                Title = HttpUtility.HtmlDecode(movie.title),
                Content = movie.synopsis,
                PosterM = movie.image.url,
                PosterS = movie.image.url,
                Vod = true,
                Feed = MovieEvent.FeedType.PaidVod,
                VodLink = GetFullUrl(movie.link.url),
                Type = 1,
                ExternalId = movie.titleID,
                StartTime = dateTime,
                EndTime = null,
                Duration = duration,
                Year = year
            });
        }

        return movieEvents;
    }

    private string GetFullUrl(string url)
    {
        if (Uri.TryCreate(httpClient.BaseAddress, url, out Uri uri))
            return uri.ToString();
        else
            return null;
    }

    private class Json
    {
        public class Props
        {
            public class Collection
            {
                public class Item
                {
                    public class Url
                    {
                        public string url { get; set; }
                    }

                    public class WatchlistAction
                    {
                        public string formatCode { get; set; }
                    }

                    public Url image { get; set; }
                    public Url link { get; set; }
                    public string title { get; set; }
                    public string titleID { get; set; }
                    public string releaseYear { get; set; }
                    public string runtime { get; set; }
                    public string synopsis { get; set; }
                    public WatchlistAction watchlistAction { get; set; }
                }

                public List<Item> items { get; set; }
            }

            public List<Collection> collections { get; set; }
        }

        public Props props { get; set; }
    }

    private async Task<Stream> GetStream()
    {
        var localDownloadOverride = primeVideoServiceOptions.LocalDownloadOverride;
        logger.LogInformation("Using LocalDownloadOverride {LocalDownloadOverride}", localDownloadOverride);
        if (localDownloadOverride != null)
        {
            return File.OpenRead(localDownloadOverride);
        }
        else
        {
            var response = await httpClient.GetAsync("/storefront/ref=atv_nb_lcl_nl_BE?ie=UTF8");
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStreamAsync();
        }
    }

    private async Task<IList<Json.Props.Collection.Item>> GetMovieInfo()
    {
        using Stream stream = await GetStream();
        HtmlParser parser = new HtmlParser();
        var document = await parser.ParseDocumentAsync(stream);

        var jsonText = document
            .QuerySelectorAll("script")
            .OfType<IHtmlScriptElement>()
            .Where(s => s.Type == "text/template")
            .Select(s => s.Text)
            .OrderByDescending(s => s.Length)
            .FirstOrDefault();

        var json = JsonSerializer.Deserialize<Json>(jsonText);
        var items = json.props.collections
            .SelectMany(c => c.items)
            .Where(i => i.watchlistAction?.formatCode == "mv");

        return items.ToList();
    }
}
