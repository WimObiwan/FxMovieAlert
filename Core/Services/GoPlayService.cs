using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using AngleSharp.Html.Parser;
using FxMovies.Core.Entities;
using Microsoft.Extensions.Logging;

namespace FxMovies.Core.Services
{
    public class GoPlayService : IMovieEventService
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

        public string ProviderName => "GoPlay";

        public string ChannelCode => "goplay";

        public async Task<IList<MovieEvent>> GetMovieEvents()
        {
            // https://github.com/timrijckaert/vrtnu-vtmgo-goplay-service/tree/master/vtmgo/src/main/java/be/tapped/vtmgo/content

            var client = httpClientFactory.CreateClient("goplay");
            var response = await client.GetAsync("/programmas/");
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
                        var episode = dataProgram.playlists?.SelectMany(p => p.episodes).FirstOrDefault();
                        if (episode == null)
                        {
                            logger.LogWarning("Skipping data-program without playlist or episode, Text={dataProgramText}", dataProgramText);
                            return null;
                        }
                        
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
                            Title = dataProgram.title.Trim(), 
                            Year = null, 
                            Vod = true,
                            Feed = MovieEvent.FeedType.FreeVod,
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
                        logger.LogWarning(x, "Skipping line with parsing exception, Text={dataProgramText}", dataProgramText);
                        throw;
                    }
                })
                .Where(e => e != null)
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