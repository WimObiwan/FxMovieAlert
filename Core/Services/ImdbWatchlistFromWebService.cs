using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FxMovies.Core.Entities;

namespace FxMovies.Core.Services;

public interface IImdbWatchlistFromWebService
{
    Task<IList<ImdbWatchlist>> GetWatchlistAsync(string imdbUserId);
}

public class ImdbWatchlistFromWebService : IImdbWatchlistFromWebService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ImdbWatchlistFromWebService(
        IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IList<ImdbWatchlist>> GetWatchlistAsync(string imdbUserId)
    {
        var jsonData = await GetDocumentString(imdbUserId);
        return GetWatchlist(jsonData);
    }

    private async Task<JsonData> GetDocumentString(string imdbUserId)
    {
        var client = _httpClientFactory.CreateClient("imdb");
        var response = await client.GetAsync($"/user/{imdbUserId}/watchlist?sort=date_added%2Cdesc&view=detail");
        response.EnsureSuccessStatusCode();
        // Troubleshoot: Debug console: 
        //   response.Content.ReadAsStringAsync().Result,nq 
        // ==> nq = non-quoted

        string text;
        await using var stream = await response.Content.ReadAsStreamAsync();
        using (TextReader textReader = new StreamReader(stream))
        {
            text = await textReader.ReadToEndAsync();
        }

        var jsonString = Regex.Match(text, @"IMDbReactInitialState\.push\(({.*})\);").Groups[1].Value;
        return JsonSerializer.Deserialize<JsonData>(jsonString);
    }

    private IList<ImdbWatchlist> GetWatchlist(JsonData jsonData)
    {
        return jsonData.list.items.Select(i =>
        {
            jsonData.titles.TryGetValue(i.imdbMovieId, out var title);
            DateTime.TryParseExact(i.added, "dd MMM yyyy", CultureInfo.GetCultureInfo("en-GB"),
                DateTimeStyles.AllowWhiteSpaces, out var dateTimeAdded);
            return new ImdbWatchlist
            {
                ImdbId = i.imdbMovieId,
                Title = title?.primary?.title,
                Date = dateTimeAdded
            };
        }).ToList();
    }

    #region JsonModel

    // Resharper disable All

    private class JsonData
    {
        public List list { get; set; }
        public Dictionary<string, Title> titles { get; set; }
    }

    private class List
    {
        public List<Item> items { get; set; }
    }

    private class Item
    {
        public string added { get; set; }
        [JsonPropertyName("const")] public string imdbMovieId { get; set; }
    }

    private class Title
    {
        public Primary primary { get; set; }
    }

    private class Primary
    {
        public string title { get; set; }
    }

    // Resharper restore All

    #endregion
}