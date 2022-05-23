using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FxMovies.Core.Entities;
using Microsoft.Extensions.Logging;

namespace FxMovies.Core.Services;

public class VrtNuService : IMovieEventService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<VrtNuService> _logger;

    private static readonly Uri BaseUrl = new Uri("https://www.vrt.be");

    public VrtNuService(
        ILogger<VrtNuService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public string ProviderName => "VrtNu";

    public string ChannelCode => "vrtnu";

    public async Task<IList<MovieEvent>> GetMovieEvents()
    {
        // https://vrtnu-api.vrt.be/suggest?facets[categories]=films

        var channel = new Channel
        {
            Code = "vrtnu",
            Name = "VRT NU",
            LogoS = "https://www.filmoptv.be/images/vrtnu.png"
        };

        var movies = await GetSuggestMovieInfo();

        var movieEvents = new List<MovieEvent>();
        foreach (var movie in movies)
        {
            Thread.Sleep(500);

            var movieDetails = await GetSearchMovieInfo(movie.programUrl);
            int? year = movieDetails.data?.program?.seasons?.Select(s => 
            {
                int? year;
                if (int.TryParse(s.name, out var year2)
                    && year2 >= 1930 && year2 <= DateTime.Now.Year)
                    year = year2;
                else
                    year = null;
                return year;
            }).Where(y => y != null).FirstOrDefault();

            DateTime? endTime = null;
            var announcement = movieDetails.data?.program?.announcement?.value;
            if (announcement != null)
            {
                Match match;
                match = Regex.Match(announcement, @"^Nog (\d+) dagen beschikbaar$");
                if (match.Success)
                {
                    endTime = DateTime.Today.AddDays(1 + int.Parse(match.Groups[1].Value)).AddMinutes(-1);
                }
                else if (announcement == "Langer dan een jaar beschikbaar")
                {
                    endTime = DateTime.Today.AddYears(1);
                }
                else
                {
                    match = Regex.Match(announcement, @"^Beschikbaar tot (?:\w+ )?(\d+)/(\d+)(?:/(\d+))?$");
                    if (match.Success)
                    {
                        if (match.Groups[3].Success)
                        {
                            endTime = DateTime.ParseExact($"{match.Groups[1].Value}/{match.Groups[2].Value}/{match.Groups[3].Value}", "dd/MM/yyyy", null);
                        }
                        else
                        {
                            endTime = DateTime.ParseExact($"{match.Groups[1].Value}/{match.Groups[2].Value}", "dd/MM", null);
                        }
                    }
                    else
                    {
                        endTime = DateTime.Today.AddDays(1).AddMinutes(-1);
                    }
                }
            }

            int type = 1; // 1 = movie, 2 = short movie, 3 = serie
            if (movieDetails.tags?.Any(t => string.Compare(t.name, "kortfilm", true) == 0) ?? false)
                type = 2;

            movieEvents.Add(new MovieEvent
            {
                Channel = channel,
                Title = movieDetails.title,
                Content = movieDetails.description ?? movieDetails.image.alt,
                PosterM = GetFullUrl(movieDetails.image.src),
                PosterS = GetFullUrl(movieDetails.image.src),
                Vod = true,
                Feed = MovieEvent.FeedType.FreeVod,
                VodLink = GetFullUrl(movieDetails.reference.link),
                Type = type,
                ExternalId = movieDetails.data.program.id,
                EndTime = endTime ?? DateTime.Today.AddYears(1),
                Duration = null,
                Year = year
            });
        }

        return movieEvents;
    }

    private string GetFullUrl(string url)
    {
        return new Uri(BaseUrl, url).AbsoluteUri;
    }

    private async Task<IList<SuggestMovieInfo>> GetSuggestMovieInfo()
    {
        // https://search7.vrt.be/suggest?facets[categories]=films

        var client = _httpClientFactory.CreateClient("vrtnu");
        //var responseObject = await client.GetFromJsonAsync<DpgCatalogResponse>("/vtmgo/catalog?pageSize=2000");
        var response = await client.GetAsync("/suggest?facets[categories]=films");
        response.EnsureSuccessStatusCode();
        // Troubleshoot: Debug console: 
        //   response.Content.ReadAsStringAsync().Result,nq 
        // ==> nq = non-quoted

        var responseObject = await response.Content.ReadFromJsonAsync<List<SuggestMovieInfo>>();
        return responseObject;
    }

    private async Task<Details> GetSearchMovieInfo(string programUrl)
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

        return responseObject?.details;
    }

    #region JsonModel

    // Resharper disable All

    private class SuggestMovieInfo
    {
        public string programUrl { get; set; }
    }

    private class Model
    {
        public Details details { get; set; }
    }

    private class Image
    {
        public string src { get; set; }
        public string alt { get; set; }
    }

    private class Data
    {
        public Program program { get; set; }
    }

    private class Program
    {
        public string id { get; set; }
        public Anouncement announcement { get; set; }
        public List<Season> seasons { get; set; }
    }

    public class Anouncement
    {
        public string value { get; set; }
    }

    private class Details
    {
        public Reference reference { get; set; }
        public string title { get; set; }
        public string description { get; set; }
        public Image image { get; set; }
        public Data data { get; set; }
        public List<Tag> tags { get; set; }
    }

    private class Reference
    {
        public string link { get; set; }
    }

    private class Season
    {
        public string name { get; set; }
    }

    private class Tag
    {
        public string category { get; set; }
        public string name { get; set; }
        public string title { get; set; }
    }

    #endregion
}