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
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<VrtNuService> logger;

    public VrtNuService(
        ILogger<VrtNuService> logger,
        IHttpClientFactory httpClientFactory)
    {
        this.logger = logger;
        this.httpClientFactory = httpClientFactory;
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
            if (movieDetails.duration < 75)
            {
                logger.LogWarning("Skipped {Program}, duration {Duration} too small",
                    movieDetails.program, movieDetails.duration);
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
                Title = movieDetails.program,
                Content = movieDetails.programDescription,
                PosterM = GetFullUrl(movieDetails.programImageUrl),
                PosterS = GetFullUrl(movieDetails.programImageUrl),
                Vod = true,
                Feed = MovieEvent.FeedType.FreeVod,
                VodLink = GetFullUrl(movieDetails.url),
                Type = 1,
                ExternalId = movieDetails.id,
                StartTime = DateTime.Parse(movieDetails.assetOnTime),
                EndTime = DateTime.Parse(movieDetails.assetOffTime),
                Duration = movieDetails.duration,
                Year = year
            });
        }

        return movieEvents;
    }

    private string GetFullUrl(string url)
    {
        return $"https:{url}";
    }

    private async Task<IList<SuggestMovieInfo>> GetSuggestMovieInfo()
    {
        var client = httpClientFactory.CreateClient("vrtnu");
        //var responseObject = await client.GetFromJsonAsync<DpgCatalogResponse>("/vtmgo/catalog?pageSize=2000");
        var response = await client.GetAsync("/suggest?facets[categories]=films");
        response.EnsureSuccessStatusCode();
        // Troubleshoot: Debug console: 
        //   response.Content.ReadAsStringAsync().Result,nq 
        // ==> nq = non-quoted

        var responseObject = await response.Content.ReadFromJsonAsync<List<SuggestMovieInfo>>();
        return responseObject;
    }

    private async Task<SearchMovieInfo> GetSearchMovieInfo(string programUrl)
    {
        // https://vrtnu-api.vrt.be/search?facets[programUrl]=//www.vrt.be/vrtnu/a-z/everybody-knows/

        var client = httpClientFactory.CreateClient("vrtnu");
        //var responseObject = await client.GetFromJsonAsync<DpgCatalogResponse>("/vtmgo/catalog?pageSize=2000");
        var response = await client.GetAsync($"/search?facets[programUrl]={programUrl}");
        response.EnsureSuccessStatusCode();
        // Troubleshoot: Debug console: 
        //   response.Content.ReadAsStringAsync().Result,nq 
        // ==> nq = non-quoted

        var responseObject = await response.Content.ReadFromJsonAsync<SearchResult>();
        return responseObject.results?.FirstOrDefault();
    }

    private class SuggestMovieInfo
    {
        public string programUrl { get; set; }
    }

    private class SearchResult
    {
        public List<SearchMovieInfo> results { get; set; }
    }

    private class SearchMovieInfo
    {
        public string title { get; set; }
        public string program { get; set; }
        public string programDescription { get; set; }
        public string programImageUrl { get; set; }
        public string url { get; set; }
        public string id { get; set; }
        public string assetOnTime { get; set; }
        public string assetOffTime { get; set; }
        public int duration { get; set; }
        public string seasonName { get; set; }
    }
}