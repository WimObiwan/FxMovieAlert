using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
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

            var movieDetails = await GetSearchMovieInfo(movie.programName);
            if (movieDetails.duration < 75)
            {
                _logger.LogWarning("Skipped {Program}, duration {Duration} too small",
                    movieDetails.programTitle, movieDetails.duration);
                continue;
            }

            var seasonName = movieDetails.seasonName;

            // Skip trailers
            if (seasonName.Equals("trailer", StringComparison.CurrentCultureIgnoreCase))
                continue;

            int? year;
            if (int.TryParse(seasonName, out var year2)
                && year2 >= 1930 && year2 <= DateTime.Now.Year)
                year = year2;
            else
                year = null;

            movieEvents.Add(new MovieEvent
            {
                Channel = channel,
                Title = movieDetails.programTitle,
                Content = movieDetails.programDescription,
                PosterM = GetFullUrl(movieDetails.programImageUrl),
                PosterS = GetFullUrl(movieDetails.programImageUrl),
                Vod = true,
                Feed = MovieEvent.FeedType.FreeVod,
                VodLink = GetFullUrl(movieDetails.url),
                Type = 1,
                ExternalId = movieDetails.id,
                StartTime = DateTime.Parse(movieDetails.onTime),
                EndTime = DateTime.Parse(movieDetails.offTime),
                Duration = movieDetails.duration,
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

    private async Task<SearchMovieInfo> GetSearchMovieInfo(string programName)
    {
        // https://vrtnu-api.vrt.be/search?facets[programUrl]=//www.vrt.be/vrtnu/a-z/everybody-knows/

        var client = _httpClientFactory.CreateClient("vrtnu");
        //var responseObject = await client.GetFromJsonAsync<DpgCatalogResponse>("/vtmgo/catalog?pageSize=2000");
        var response = await client.GetAsync($"/search?facets[programName]={programName}");
        response.EnsureSuccessStatusCode();
        // Troubleshoot: Debug console: 
        //   response.Content.ReadAsStringAsync().Result,nq 
        // ==> nq = non-quoted

        var responseObject = await response.Content.ReadFromJsonAsync<SearchResult>() ??
                             throw new Exception("Response is missing");

        return responseObject.results?.FirstOrDefault();
    }

    #region JsonModel

    // Resharper disable All

    private class SuggestMovieInfo
    {
        public string programName { get; set; }
    }

    private class SearchResult
    {
        public List<SearchMovieInfo> results { get; set; }
    }

    private class SearchMovieInfo
    {
        public string title { get; set; }
        public string programTitle { get; set; }
        public string programDescription { get; set; }
        public string programImageUrl { get; set; }
        public string url { get; set; }
        public string id { get; set; }
        public string onTime { get; set; }
        public string offTime { get; set; }
        public int duration { get; set; }
        public string seasonName { get; set; }
    }

    // Resharper restore All

    #endregion
}