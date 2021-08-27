using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using AngleSharp.Html.Parser;
using FxMovies.FxMoviesDB;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FxMovies.Core {
    public interface IGoPlayService
    {
        Task<IList<MovieEvent>> GetMovieEvents();
    }

    // public class VtmGoServiceOptions
    // {
    //     public static string Position => "VtmGoService";

    //     // https://vtm.be/vtmgo --> login --> F12 --> Tab "Storage" --> Cookies --> https://vtm.be --> lfvp_auth_token --> "ey...
    //     public string AuthToken { get; set; }
    //     public string Username { get; set; }
    //     public string Password { get; set; }
    // }

    public class GoPlayService : IGoPlayService
    {
        private readonly ILogger<GoPlayService> logger;
        private readonly IHttpClientFactory httpClientFactory;

        public GoPlayService(
            ILogger<GoPlayService> logger,
            IHttpClientFactory httpClientFactory)
        {
            this.logger = logger;
            this.httpClientFactory = httpClientFactory;
        }

        public async Task<IList<MovieEvent>> GetMovieEvents()
        {
            // https://github.com/timrijckaert/vrtnu-vtmgo-goplay-service/tree/master/vtmgo/src/main/java/be/tapped/vtmgo/content

            var client = httpClientFactory.CreateClient("goplay");
            var request = new HttpRequestMessage(HttpMethod.Get, "/programmas/");
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            using Stream stream = await response.Content.ReadAsStreamAsync();
            HtmlParser parser = new HtmlParser();
            var document = await parser.ParseDocumentAsync(stream);

            Channel channel = new Channel()
            {
                Code = "goplay",
                Name = "GoPlay",
                LogoS = "https://www.filmoptv.be/images/goplay.png"
            };

            return document
                .QuerySelectorAll(".poster-teaser")
                .Where(e => e.GetAttribute("data-category") == "5286") // 5286 = Film
                .Select(e => 
                {
                    string dataProgramText = e.GetAttribute("data-program");
                    try
                    {
                        var dataProgram = JsonSerializer.Deserialize<DataProgram>(dataProgramText);
                        var episode = dataProgram.playlists.SelectMany(p => p.episodes).FirstOrDefault();
                        DateTime? startTime;
                        if (episode.publishDate.ValueKind == JsonValueKind.Number && episode.publishDate.TryGetInt64(out long value))
                            startTime = DateTime.UnixEpoch.AddSeconds(value);
                        else
                            startTime = null;
                        DateTime? endTime;
                        if (episode.unpublishDate.ValueKind == JsonValueKind.Number && episode.unpublishDate.TryGetInt64(out value))
                            endTime = DateTime.UnixEpoch.AddSeconds(value);
                        else
                            endTime = null;
                        string link = episode.link;
                        if (link.StartsWith('/'))
                            link = "https://www.goplay.be" + link;
                        return new MovieEvent()
                        {
                            ExternalId = dataProgram.id,
                            Type = 1,  // 1 = movie, 2 = short movie, 3 = serie
                            Title = dataProgram.title, 
                            Year = null, 
                            Vod = true,
                            StartTime = startTime ?? DateTime.UtcNow,
                            EndTime = endTime, 
                            Channel = channel,
                            PosterS = episode.image, 
                            PosterM = episode.image,
                            Duration = (episode.duration + 30) / 60,
                            Content = episode.description,
                            VodLink = link,
                            AddedTime = DateTime.UtcNow,
                        };
                    }
                    catch (Exception x)
                    {
                        logger.LogError(x, "Failed to parse {line}", dataProgramText);
                        throw;
                    }
                })
                .ToList();
        }

        class DataProgram
        {
            public string id { get; set; }
            public string title { get; set; }
            public string description { get; set; }
            public PageInfo pageInfo { get; set; }
            public List<Playlist> playlists { get; set; }

            public class PageInfo
            {
                public string title { get; set; }
                public string type { get; set; }
            }

            public class Playlist
            {
                public List<Episode> episodes { get; set; }

                public class Episode
                {
                    public int duration { get; set; }
                    public JsonElement publishDate { get; set; }
                    public JsonElement unpublishDate { get; set; }
                    public string image { get; set; }
                    public PageInfo pageInfo { get; set; }
                    public string link { get; set; }
                    public string description { get; set; }
                }
            }
        }

        private MovieEvent TransformDataProgram(string json)
        {
            return null;
        }
    }
}