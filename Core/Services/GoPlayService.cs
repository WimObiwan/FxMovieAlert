using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using FxMovies.Core.Entities;
using Microsoft.Extensions.Logging;

namespace FxMovies.Core.Services;

public class GoPlayService : IMovieEventService
{
    private const string SiteBaseUrl = "https://www.play.tv";
    private const int SearchPageSize = 20;
    private const int MaxSearchPages = 500;

    // The search API has no "list everything" mode, it only accepts a free text query (minimum 2 characters).
    // The query terms are OR'ed and match on word prefixes, so searching for every 2 character combination
    // returns the complete program catalog in a single (paged) search.  Keep the number of terms below ~800,
    // the search backend answers with a 500 when the query expands into too many clauses.
    private static readonly string SearchAllQuery = BuildSearchAllQuery();

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
        // GoPlay was rebranded to Play (goplay.be -> play.tv), the channel code is kept for backwards compatibility.
        var channel = new Channel
        {
            Code = "goplay",
            Name = "Play",
            LogoS = "https://www.filmoptv.be/images/playtv.png"
        };

        var movieEvents = new List<MovieEvent>();

        foreach (var program in await GetFilmPrograms())
        {
            var uuid = program.uuid;
            if (uuid == null)
                continue;

            try
            {
                var details = await GetProgramDetails(uuid);

                if (details == null)
                    continue;

                if (!string.Equals(details.type, "MOVIE", StringComparison.InvariantCultureIgnoreCase))
                    continue;

                // Movies with a tvod section have to be rented or bought (Play Kinepolis), they are no free VOD.
                if (details.tvod != null)
                    continue;

                var image = GetImageUrl(details.images) ?? GetImageUrl(program.images);
                var link = details.link != null ? SiteBaseUrl + details.link : program.url;

                movieEvents.Add(new MovieEvent
                {
                    ExternalId = uuid,
                    Type = 1, // 1 = movie, 2 = short movie, 3 = serie
                    Title = (details.title ?? program.title)?.Trim(),
                    Year = null,
                    Vod = true,
                    Feed = MovieEvent.FeedType.FreeVod,
                    StartTime = GetDateTime(details.dates?.publishDate) ?? DateTime.UtcNow,
                    EndTime = GetDateTime(details.dates?.unpublishDate),
                    Channel = channel,
                    PosterS = image,
                    PosterM = image,
                    Duration = details.duration.HasValue ? details.duration.Value / 60 : null,
                    Content = details.description,
                    VodLink = link,
                    AddedTime = DateTime.UtcNow
                });
            }
            catch (Exception x)
            {
                _logger.LogWarning(x, "Skipping program with parsing exception, Uuid={uuid}", uuid);
            }
        }

        return movieEvents;
    }

    private static string BuildSearchAllQuery()
    {
        const string letters = "abcdefghijklmnopqrstuvwxyz";
        const string digits = "0123456789";

        var query = new StringBuilder();

        foreach (var first in letters)
        foreach (var second in letters)
        {
            if (query.Length > 0)
                query.Append(' ');
            query.Append(first).Append(second);
        }

        foreach (var first in digits)
        foreach (var second in digits)
            query.Append(' ').Append(first).Append(second);

        return query.ToString();
    }

    private DateTime? GetDateTime(long? date)
    {
        if (date.HasValue)
            return DateTime.UnixEpoch.AddSeconds(date.Value).ToLocalTime();
        return null;
    }

    private static string? GetImageUrl(ProgramImages? images)
    {
        return images?.landscape?.FirstOrDefault(i => i.url != null)?.url
               ?? images?.portrait?.FirstOrDefault(i => i.url != null)?.url;
    }

    private static string? GetImageUrl(SearchImages? images)
    {
        return images?.landscape ?? images?.defaultImage ?? images?.portrait;
    }

    private async Task<IList<SearchSource>> GetFilmPrograms()
    {
        var client = _httpClientFactory.CreateClient("goplay");

        var programs = new Dictionary<string, SearchSource>();
        var page = 0;
        int pageCount;

        do
        {
            var response = await client.PostAsJsonAsync("/web/v1/search", new
            {
                mode = "programs",
                page,
                query = SearchAllQuery
            });
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<SearchResult>()
                         ?? throw new Exception("Json parsing failed");

            var hits = result.hits?.hits ?? Array.Empty<SearchHit>();
            if (hits.Length == 0)
                break;

            foreach (var source in hits.Select(h => h.source))
                if (source?.uuid != null
                    && source.tracking?.item_category?.Equals("Film", StringComparison.CurrentCultureIgnoreCase) == true)
                    programs[source.uuid] = source;

            pageCount = Math.Min((result.hits!.total + SearchPageSize - 1) / SearchPageSize, MaxSearchPages);
        } while (++page < pageCount);

        return programs.Values.ToList();
    }

    private async Task<ProgramDetails?> GetProgramDetails(string uuid)
    {
        var client = _httpClientFactory.CreateClient("goplay");
        var response = await client.GetAsync($"/web/v1/programs/{uuid}");

        // The search index also contains programs that are no longer available.
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInformation("Skipping program that is not available anymore, Uuid={uuid}", uuid);
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ProgramDetails>();
    }

    #region JsonModel

    // ReSharper disable All

    private class SearchResult
    {
        public SearchHits? hits { get; set; }
    }

    private class SearchHits
    {
        public int total { get; set; }
        public SearchHit[]? hits { get; set; }
    }

    private class SearchHit
    {
        [JsonPropertyName("_source")] public SearchSource? source { get; set; }
    }

    private class SearchSource
    {
        public string? uuid { get; set; }
        public string? title { get; set; }
        public string? url { get; set; }
        public SearchImages? images { get; set; }
        public SearchTracking? tracking { get; set; }
    }

    private class SearchImages
    {
        [JsonPropertyName("default")] public string? defaultImage { get; set; }
        public string? portrait { get; set; }
        public string? landscape { get; set; }
    }

    private class SearchTracking
    {
        public string? item_category { get; set; }
    }

    private class ProgramDetails
    {
        public string? programUuid { get; set; }
        public string? type { get; set; }
        public string? title { get; set; }
        public string? category { get; set; }
        public string? description { get; set; }
        public string? link { get; set; }
        public int? duration { get; set; }
        public ProgramDates? dates { get; set; }
        public ProgramImages? images { get; set; }
        public object? tvod { get; set; }
    }

    private class ProgramDates
    {
        public long? publishDate { get; set; }
        public long? unpublishDate { get; set; }
    }

    private class ProgramImages
    {
        public ProgramImage[]? portrait { get; set; }
        public ProgramImage[]? landscape { get; set; }
    }

    private class ProgramImage
    {
        public string? style { get; set; }
        public string? url { get; set; }
    }

    // ReSharper restore All

    #endregion
}
