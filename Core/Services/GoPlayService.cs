using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using FxMovies.Core.Entities;
using Microsoft.Extensions.Logging;

namespace FxMovies.Core.Services;

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
        Channel channel = new Channel()
        {
            Code = "goplay",
            Name = "GoPlay",
            LogoS = "https://www.filmoptv.be/images/goplay.png"
        };

        var list = await GetDataProgramList();
        return list
            .Select(async dataProgram => 
            {
                string link = dataProgram.link;
                string image = image = dataProgram.images.teaser;

                if (link != null && link.StartsWith('/'))
                    link = "https://www.goplay.be" + link;

                var dataProgramDetails = await GetDataProgramDetails(link);

                return new MovieEvent()
                {
                    ExternalId = dataProgram.id,
                    Type = 1,  // 1 = movie, 2 = short movie, 3 = serie
                    Title = dataProgram.title.Trim(), 
                    Year = null, 
                    Vod = true,
                    Feed = MovieEvent.FeedType.FreeVod,
                    StartTime = GetDateTime(dataProgramDetails.pageInfo.publishDate) ?? DateTime.UtcNow,
                    EndTime = GetDateTime(dataProgramDetails.pageInfo.unpublishDate), 
                    Channel = channel,
                    PosterS = image, 
                    PosterM = image,
                    Duration = null,
                    Content = dataProgramDetails.pageInfo.description,
                    VodLink = link,
                    AddedTime = DateTime.UtcNow,
                };
            })
            .Select(t => t.Result)
            .ToList();
    }

    private DateTime? GetDateTime(int? date)
    {
        if (date.HasValue)
            return new DateTime(1970, 1, 1).AddSeconds(date.Value).ToLocalTime();
        else
            return null;
    }

    private async Task<IList<DataProgram>> GetDataProgramList()
    {
        // https://github.com/timrijckaert/vrtnu-vtmgo-goplay-service/tree/master/vtmgo/src/main/java/be/tapped/vtmgo/content

        var client = httpClientFactory.CreateClient("goplay");
        var response = await client.GetAsync("/programmas/");
        response.EnsureSuccessStatusCode();

        using Stream stream = await response.Content.ReadAsStreamAsync();
        HtmlParser parser = new HtmlParser();
        var document = await parser.ParseDocumentAsync(stream);

        return document
            .QuerySelectorAll(".poster-teaser")
            .Where(e => e.GetAttribute("data-category") == "5286") // 5286 = Film
            .Select(e => 
            {
                string dataProgramText = e.GetAttribute("data-program");
                try
                {
                    return JsonSerializer.Deserialize<DataProgram>(dataProgramText);
                }
                catch (Exception x)
                {
                    logger.LogWarning(x, "Skipping line with parsing exception, Text={dataProgramText}", dataProgramText);
                    return null;
                }
            })
            .Where(e => e != null)
            .ToList();
    }

    private async Task<DataProgram> GetDataProgramDetails(string link)
    {
        var client = httpClientFactory.CreateClient("goplay");
        var response = await client.GetAsync(link);
        response.EnsureSuccessStatusCode();

        using Stream stream = await response.Content.ReadAsStreamAsync();
        HtmlParser parser = new HtmlParser();
        var document = await parser.ParseDocumentAsync(stream);

        var dataProgramText = document
            .QuerySelectorAll("script")
            .OfType<IHtmlScriptElement>()
            .Where(s => s.Type == "application/json")
            .Select(s => s.Text)
            .OrderByDescending(s => s.Length)
            .FirstOrDefault();

        var dataProgramDetails =  JsonSerializer.Deserialize<DataProgram>(dataProgramText);
        return dataProgramDetails;
    }

    class DataProgram
    {
        public string id { get; set; }
        public string title { get; set; }
        public string link { get; set; }
        public PageInfo pageInfo { get; set; }
        public Images images { get; set; }

        public class PageInfo
        {
            public string title { get; set; }
            public string type { get; set; }
            public int? publishDate { get; set; }
            public int? unpublishDate { get; set; }
            public string description { get; set; }
        }

        public class Images
        {
            public string poster { get; set; }
            public string teaser { get; set; }
        }
    }

    private MovieEvent TransformDataProgram(string json)
    {
        return null;
    }
}
