using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FxMovies.Core.Entities;
using Microsoft.Extensions.Logging;

namespace FxMovies.Core.Services;

public interface IImdbWatchlistFromWebService
{
    Task<IList<ImdbWatchlist>> GetWatchlistAsync(string ImdbUserId);
}

public class ImdbWatchlistFromWebService : IImdbWatchlistFromWebService
{
    private readonly ILogger<ImdbWatchlistFromWebService> logger;
    private readonly IHttpClientFactory httpClientFactory;

    public ImdbWatchlistFromWebService(
        ILogger<ImdbWatchlistFromWebService> logger,
        IHttpClientFactory httpClientFactory)
    {
        this.logger = logger;
        this.httpClientFactory = httpClientFactory;
    }

    class JsonData
    {
        public List list { get; set; }
        public Dictionary<string, Title> titles { get; set; }
    }

    class List
    {
        public List<Item> items { get; set; }
    }

    class Item
    {
        public string added { get; set; }
        [JsonPropertyName("const")]
        public string imdbMovieId { get; set; }
    }

    class Title
    {
        public Primary primary { get; set; }
    }

    class Primary
    {
        public string title { get; set; }
    }

    public async Task<IList<ImdbWatchlist>> GetWatchlistAsync(string imdbUserId)
    {
        var jsonData = await GetDocumentString(imdbUserId);
        return GetWatchlist(jsonData);
    }

    private async Task<JsonData> GetDocumentString(string imdbUserId)
    {
        var client = httpClientFactory.CreateClient("imdb");
        var response = await client.GetAsync($"/user/{imdbUserId}/watchlist?sort=date_added%2Cdesc&view=detail");
        response.EnsureSuccessStatusCode();
        // Troubleshoot: Debug console: 
        //   response.Content.ReadAsStringAsync().Result,nq 
        // ==> nq = non-quoted

        string text;
        using (Stream stream = await response.Content.ReadAsStreamAsync())
        using (TextReader textReader = new StreamReader(stream))
        {
            text = await textReader.ReadToEndAsync();
        }
        var jsonString = Regex.Match(text, @"IMDbReactInitialState\.push\(({.*})\);").Groups[1].Value;
        return System.Text.Json.JsonSerializer.Deserialize<JsonData>(jsonString);
    }

    private IList<ImdbWatchlist> GetWatchlist(JsonData jsonData)
    {
        return jsonData.list.items.Select(i =>
        {
            jsonData.titles.TryGetValue(i.imdbMovieId, out Title title);
            DateTime.TryParseExact(i.added, "dd MMM yyyy", CultureInfo.GetCultureInfo("en-GB"), 
                DateTimeStyles.AllowWhiteSpaces, out DateTime dateTimeAdded);
            return new ImdbWatchlist()
            {
                ImdbId = i.imdbMovieId,
                Title = title?.primary?.title,
                Date = dateTimeAdded
            };
        }).ToList();
    }
}
