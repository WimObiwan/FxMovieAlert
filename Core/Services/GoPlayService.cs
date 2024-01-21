using System;
using System.Collections.Generic;
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
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GoPlayService> _logger;

    public GoPlayService(
        ILogger<GoPlayService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public string ProviderName => "GoPlay";

    public string ProviderCode => "goplay";
    public IList<string> ChannelCodes => new List<string>() { "goplay" };

    public async Task<IList<MovieEvent>> GetMovieEvents()
    {
        var channel = new Channel
        {
            Code = "goplay",
            Name = "GoPlay",
            LogoS = "https://www.filmoptv.be/images/goplay.png"
        };

        var list = await GetDataProgramList();
        return list
            .Select(async dataProgram =>
            {
                var link = dataProgram.link;
                var image = dataProgram.images?.teaser;

                if (link != null)
                {
                    if (link.StartsWith('/'))
                        link = "https://www.goplay.be" + link;

                    try
                    {
                        var dataProgramDetails = await GetDataProgramDetails(link);

                        return new MovieEvent
                        {
                            ExternalId = dataProgram.id,
                            Type = 1, // 1 = movie, 2 = short movie, 3 = serie
                            Title = dataProgram.title?.Trim(),
                            Year = null,
                            Vod = true,
                            Feed = MovieEvent.FeedType.FreeVod,
                            StartTime = GetDateTime(dataProgramDetails.pageInfo?.publishDate) ?? DateTime.UtcNow,
                            EndTime = GetDateTime(dataProgramDetails.pageInfo?.unpublishDate),
                            Channel = channel,
                            PosterS = image,
                            PosterM = image,
                            Duration = dataProgramDetails.movie?.duration,
                            Content = dataProgramDetails.pageInfo?.description,
                            VodLink = link,
                            AddedTime = DateTime.UtcNow
                        };
                    }
                    catch (Exception x)
                    {
                        _logger.LogWarning(x, "Skipping event with parsing exception, Url={link}",
                            link);
                    }
                }

                return null;
            })
            .Select(t => t?.Result)
            .Where(me => me != null)
            .Select(me => me!)
            .ToList();
    }

    private DateTime? GetDateTime(int? date)
    {
        if (date.HasValue)
            return new DateTime(1970, 1, 1).AddSeconds(date.Value).ToLocalTime();
        return null;
    }

    private async Task<IList<DataProgram>> GetDataProgramList()
    {
        // https://github.com/timrijckaert/vrtnu-vtmgo-goplay-service/tree/master/vtmgo/src/main/java/be/tapped/vtmgo/content

        var client = _httpClientFactory.CreateClient("goplay");
        var response = await client.GetAsync("/programmas/");
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        var parser = new HtmlParser();
        var document = await parser.ParseDocumentAsync(stream);

        return document
            .QuerySelectorAll(".poster-teaser")
            .Where(e => e.GetAttribute("data-category") == "5286") // 5286 = Film
            .Select(e =>
            {
                string? dataProgramText = null;
                try
                {
                    dataProgramText = e.GetAttribute("data-program") ??
                                      throw new Exception("Entry contains no data-program");
                    return JsonSerializer.Deserialize<DataProgram>(dataProgramText);
                }
                catch (Exception x)
                {
                    _logger.LogWarning(x, "Skipping line with parsing exception, Text={dataProgramText}",
                        dataProgramText);
                    return null;
                }
            })
            .Where(e => e != null)
            .Select(e => e!)
            .ToList();
    }

    private async Task<DataProgram> GetDataProgramDetails(string link)
    {
        var client = _httpClientFactory.CreateClient("goplay");
        var response = await client.GetAsync(link);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        var parser = new HtmlParser();
        var document = await parser.ParseDocumentAsync(stream);

        var data = document
            .QuerySelectorAll("div")
            .Select(d => d.GetAttribute("data-hero"))
            .Where(a => a != null)
            .OrderByDescending(s => s!.Length)
            .FirstOrDefault();

        if (data != null)
        {
            var data2 = 
                JsonSerializer.Deserialize<Data>(data);
            if (data2?.data != null)
                return data2.data;
        }

        var dataProgramText = document
                                  .QuerySelectorAll("script")
                                  .OfType<IHtmlScriptElement>()
                                  .Where(s => s.Type == "application/json")
                                  .Select(s => s.Text)
                                  .OrderByDescending(s => s.Length)
                                  .FirstOrDefault()
                              ?? throw new Exception("Entry contains no json");

        return JsonSerializer.Deserialize<DataProgram>(dataProgramText) ?? throw new Exception("DataProgram missing");
    }

    #region JsonModel

    // ReSharper disable All

    private class Data
    {
        public DataProgram? data { get; set; }
    }

    private class DataProgram
    {
        public string? id { get; set; }
        public string? title { get; set; }
        public string? link { get; set; }
        public PageInfo? pageInfo { get; set; }
        public Images? images { get; set; }
        public Movie? movie { get; set; }

        public class PageInfo
        {
            public string? title { get; set; }
            public string? type { get; set; }
            public int? publishDate { get; set; }
            public int? unpublishDate { get; set; }
            public string? description { get; set; }
        }

        public class Images
        {
            public string? poster { get; set; }
            public string? teaser { get; set; }
        }

        public class Movie
        {
            public int? duration { get; set; }
        }
    }

    // ReSharper restore All

    #endregion
}