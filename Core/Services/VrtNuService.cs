using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp.Html.Parser;
using FxMovies.Core.Entities;

namespace FxMovies.Core.Services;

public class VrtNuService : IMovieEventService
{
    private static readonly Uri BaseUrl = new("https://www.vrt.be");
    private readonly IHttpClientFactory _httpClientFactory;

    public VrtNuService(
        IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    // Only keep first MaxCount MovieEvents for performance reasons during testing (Design for Testability)
    public int MaxCount { get; set; } = 1024;

    public string ProviderName => "VrtNu";

    public string ChannelCode => "vrtnu";

    public async Task<IList<MovieEvent>> GetMovieEvents()
    {
        // https://vrtnu-api.vrt.be/suggest?facets[categories]=films

        var channel = new Channel
        {
            Code = "vrtnu",
            Name = "VRT MAX",
            LogoS = "https://www.filmoptv.be/images/vrtmax.png"
        };

        var movies = await GetSuggestMovieInfo();

        var movieEvents = new List<MovieEvent>();
        foreach (var movie in movies)
        {
            Thread.Sleep(500);

            var movieModel = await GetSearchMovieInfo(movie);
            var movieDetails = movieModel.details ?? throw new Exception("Details is missing");
            var year = movieDetails.data?.program?.seasons?.Select(s =>
            {
                int? year;
                if (int.TryParse(s.name, out var year2)
                    && year2 >= 1930 && year2 <= DateTime.Now.Year)
                    year = year2;
                else
                    year = null;
                return year;
            }).FirstOrDefault(y => y != null);

            DateTime? endTime = null;
            var announcement = movieDetails.data?.program?.announcement?.value;
            if (announcement != null)
            {
                var match = Regex.Match(announcement, @"^Nog (\d+) dagen beschikbaar$");
                if (match.Success)
                {
                    endTime = DateTime.Today.AddDays(1 + int.Parse(match.Groups[1].Value)).AddMinutes(-1);
                }
                else if (announcement == "Langer dan een jaar beschikbaar")
                {
                    endTime = DateTime.Today.AddYears(1);
                }
                else if (Regex.IsMatch(announcement, @"^Vanaf .*$"))
                {
                    // Skip movies that are announced to be available in the future
                    continue;
                }
                else
                {
                    match = Regex.Match(announcement, @"^Beschikbaar tot (?:\w+ )?(\d+)/(\d+)(?:/(\d+))?$");
                    if (match.Success)
                    {
                        if (match.Groups[3].Success)
                            endTime = DateTime.ParseExact(
                                $"{match.Groups[1].Value}/{match.Groups[2].Value}/{match.Groups[3].Value}",
                                "dd/MM/yyyy", null);
                        else
                            endTime = DateTime.ParseExact($"{match.Groups[1].Value}/{match.Groups[2].Value}", "dd/MM",
                                null);
                    }
                    else
                    {
                        endTime = DateTime.Today.AddDays(1).AddMinutes(-1);
                    }
                }
            }

            var type = 1; // 1 = movie, 2 = short movie, 3 = serie
            if (movieDetails.tags?.Any(t => string.Compare(t.name, "kortfilm", StringComparison.InvariantCultureIgnoreCase) == 0) ?? false)
                type = 2;

            var imageUrl = movieDetails.image?.src == null ? null : GetFullUrl(movieDetails.image.src);
            var vodUrl = movieDetails.reference?.link == null ? null : GetFullUrl(movieDetails.reference.link);

            var durationText = movieModel
                .items
                ?.parsys
                ?.items
                ?.container
                ?.items
                ?.episodesList
                ?.items
                ?.FirstOrDefault()
                .Value
                .episodes
                ?.FirstOrDefault()
                .Value
                .mediaMeta
                ?.FirstOrDefault()
                ?.value;

            int? duration = null;
            if (durationText != null)
            {
                var match = Regex.Match(durationText, @"^(\d+) min$");
                if (match.Success) duration = int.Parse(match.Groups[1].Value);
            }

            movieEvents.Add(new MovieEvent
            {
                Channel = channel,
                Title = movieDetails.title,
                Content = movieDetails.description ?? movieDetails.image?.alt,
                PosterM = imageUrl,
                PosterS = imageUrl,
                Vod = true,
                Feed = MovieEvent.FeedType.FreeVod,
                VodLink = vodUrl,
                Type = type,
                ExternalId = movieDetails.data?.program?.id,
                EndTime = endTime ?? DateTime.Today.AddYears(1),
                Duration = duration,
                Year = year
            });
        }

        return movieEvents;
    }

    private string GetFullUrl(string url)
    {
        return new Uri(BaseUrl, url).AbsoluteUri;
    }

    private async Task<IList<string>> GetSuggestMovieInfo()
    {
        // https://www.vrt.be/vrtnu/a-z/

        var client = _httpClientFactory.CreateClient("vrtnu");
        //var responseObject = await client.GetFromJsonAsync<DpgCatalogResponse>("/vtmgo/catalog?pageSize=2000");
        var response = await client.GetAsync("");
        response.EnsureSuccessStatusCode();
        // Troubleshoot: Debug console: 
        //   response.Content.ReadAsStringAsync().Result,nq 
        // ==> nq = non-quoted

        await using var stream = await response.Content.ReadAsStreamAsync();

        var parser = new HtmlParser();
        var document = await parser.ParseDocumentAsync(stream);
        return
            document
                .GetElementsByTagName("nui-tile")
                .Where(e =>
                    // metadata = "brands:een;categories:,films,humor,een;title:8eraf"
                    e
                        .Attributes["metadata"]
                        ?.Value
                        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(ms =>
                        {
                            var msi = ms.Split(':', 2,
                                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                            if (msi.Length == 2)
                                return new Tuple<string, string>(msi[0], msi[1]);
                            return null;
                        })
                        .FirstOrDefault(t =>
                            t?.Item1.Equals("categories", StringComparison.InvariantCultureIgnoreCase) ?? false)
                        ?.Item2
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Any(c => c.Equals("films", StringComparison.InvariantCultureIgnoreCase))
                        ?? false
                )
                .Select(e => e.Attributes["href"]?.Value)
                .Where(v => !string.IsNullOrEmpty(v))
                .Select(v => v!)
                .Distinct()
                .Take(MaxCount)
                .ToList();
    }

    private async Task<Model> GetSearchMovieInfo(string programUrl)
    {
        // https://vrtnu-api.vrt.be/search?facets[programUrl]=//www.vrt.be/vrtnu/a-z/everybody-knows/

        var client = _httpClientFactory.CreateClient("vrtnu");
        //var responseObject = await client.GetFromJsonAsync<DpgCatalogResponse>("/vtmgo/catalog?pageSize=2000");
        programUrl = Regex.Replace(programUrl, "/$", ".model.json");
        var response = await client.GetAsync(programUrl);
        response.EnsureSuccessStatusCode();
        // Troubleshoot: Debug console: 
        //   response.Content.ReadAsStringAsync().Result,nq 
        // ==> nq = non-quoted

        var responseObject = await response.Content.ReadFromJsonAsync<Model>() ??
                             throw new Exception("Response is missing");

        return responseObject;
    }

    #region JsonModel

    // Resharper disable All

    private class Model
    {
        public Details? details { get; set; }

        [JsonPropertyName(":items")] public Items1? items { get; set; }
    }

    private class Image
    {
        public string? src { get; set; }
        public string? alt { get; set; }
    }

    private class Data
    {
        public Program? program { get; set; }
    }

    private class Program
    {
        public string? id { get; set; }
        public Anouncement? announcement { get; set; }
        public List<Season>? seasons { get; set; }
    }

    public class Anouncement
    {
        public string? value { get; set; }
    }

    private class Details
    {
        public Reference? reference { get; set; }
        public string? title { get; set; }
        public string? description { get; set; }
        public Image? image { get; set; }
        public Data? data { get; set; }
        public List<Tag>? tags { get; set; }
    }

    private class Reference
    {
        public string? link { get; set; }
    }

    private class Season
    {
        public string? name { get; set; }
    }

    private class Tag
    {
        public string? category { get; set; }
        public string? name { get; set; }
        public string? title { get; set; }
    }

    private class Items1
    {
        public Parsys? parsys { get; set; }
    }

    private class Parsys
    {
        [JsonPropertyName(":items")] public Items2? items { get; set; }
    }

    private class Items2
    {
        public Container? container { get; set; }
    }

    private class Container
    {
        [JsonPropertyName(":items")] public Items3? items { get; set; }
    }

    private class Items3
    {
        [JsonPropertyName("episodes-list")] public EpisodesList? episodesList { get; set; }
    }

    private class EpisodesList
    {
        [JsonPropertyName(":items")] public Dictionary<string, Season2>? items { get; set; }
    }

    private class Season2
    {
        [JsonPropertyName(":items")] public Dictionary<string, Episode>? episodes { get; set; }
    }

    private class Episode
    {
        public MediaMeta[]? mediaMeta { get; set; }
    }

    private class MediaMeta
    {
        public string? type { get; set; }
        public string? value { get; set; }
    }

    #endregion
}