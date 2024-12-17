using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
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

        var list = await GetDataList();
        return list
            .Select(async dataProgram =>
            {
                var link = dataProgram.data?.path;
                var image = dataProgram.data?.images?.posterLandscape ?? dataProgram.data?.images?.poster;

                if (link != null)
                {
                    if (link.StartsWith('/'))
                        link = "https://www.goplay.be/video" + link;

                    try
                    {
                        var dataProgramDetails = await GetDataProgramDetails(link);

                        if (!(dataProgramDetails.program?.published == true 
                            && dataProgramDetails.program.type == "program" 
                            && dataProgramDetails.program.subtype == "movie"))
                            return null;

                        return new MovieEvent
                        {
                            ExternalId = dataProgram.uuid,
                            Type = 1, // 1 = movie, 2 = short movie, 3 = serie
                            Title = dataProgram.data?.title?.Trim(),
                            Year = null,
                            Vod = true,
                            Feed = MovieEvent.FeedType.FreeVod,
                            StartTime = GetDateTime(dataProgramDetails.datePublished) ?? DateTime.UtcNow,
                            EndTime = GetDateTime(dataProgramDetails.dateUnpublished),
                            Channel = channel,
                            PosterS = image,
                            PosterM = image,
                            // Duration = dataProgramDetails.movie?.duration,
                            Content = dataProgramDetails.videoDescription,
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

    private async Task<IList<ProgramData>> GetDataList()
    {
        // https://github.com/timrijckaert/vrtnu-vtmgo-goplay-service/tree/master/vtmgo/src/main/java/be/tapped/vtmgo/content

        // https://www.goplay.be/programmas
        var client = _httpClientFactory.CreateClient("goplay");
        var response = await client.GetAsync("/programmas?categorie=film");
        response.EnsureSuccessStatusCode();

        string text = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(text, """\\"brand\\":\\".+?\\",\\"results\\":(.+),\\"categories\\":""");
        text = JsonSerializer.Deserialize<string>('"' + match.Groups[1].Value + '"') ??  throw new Exception("Json parsing failed");

        var data = JsonSerializer.Deserialize<ProgramData[]>(text) ??  throw new Exception("Json parsing failed");
        
        return data
            .Where(e => e.data?.categoryName?.Equals("Film", StringComparison.CurrentCultureIgnoreCase) == true)
            .ToList();
    }

    private async Task<ProgramDataDetails> GetDataProgramDetails(string link)
    {
        var client = _httpClientFactory.CreateClient("goplay");
        var response = await client.GetAsync(link);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        var parser = new HtmlParser();
        var document = await parser.ParseDocumentAsync(stream);

        // // https://brightdata.com/blog/how-tos/web-scraping-with-next-js
        // var scriptNodes = document.QuerySelectorAll("script");
        // var hydrationScriptNodes = scriptNodes.Where(e => e.InnerHtml.Contains("self.__next_f.push"));

        // var scriptNode = hydrationScriptNodes.Where(e => e.InnerHtml.Contains("initialTree"));

        string text = await response.Content.ReadAsStringAsync();
        //var match = Regex.Match(text, """{\\"meta\\":(.+?)}]]\\n""");
        //var match = Regex.Match(text, """{\\"video\\":(.+?)\\n""");
        var match = Regex.Match(text, """{\\"video\\":(.+?),\\"videoId\\":""");
        text = match.Groups[1].Value;
        text = text.Replace("\"])</script><script>self.__next_f.push([1,\"", "");
        text = JsonSerializer.Deserialize<string>('"' + text + '"') ??  throw new Exception("Json parsing failed");

        var data = JsonSerializer.Deserialize<ProgramDataDetails>(text) ??  throw new Exception("Json parsing failed");
        return data;
    }

    #region JsonModel

    // ReSharper disable All

    private class ProgramData
    {
        public Data? data { get; set; }
        public string? uuid { get; set; }
    }

    private class Data
    {
        public string? brandName { get; set; }
        public string? categoryName { get; set; }
        public string? title { get; set; }
        public string? path { get; set; }
        public string? parentalRating { get; set; }
        public Images? images { get; set; }
    }

    private class Images
    {
        public string? poster { get; set; }
        public string? posterLandscape { get; set; }
    }

    private class ProgramDataDetails
    {
        public string? description { get; set; }
        public string? videoDescription { get; set; }
        public int? datePublished { get; set; }
        public int? dateUnpublished { get; set; }
        public Program? program { get; set; }
    }

    private class Program
    {
        public bool? published { get; set; }
        public string? type { get; set; }
        public string? subtype { get; set; }
    }

    // ReSharper restore All

    #endregion
}