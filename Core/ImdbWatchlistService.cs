using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using FxMovies.FxMoviesDB;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FxMovies.Core
{
    public class ImdbWatchlist
    {
        public string ImdbId { get; set; }
        public DateTime Date { get; set; }
        public string Title { get; set; }
    }

    public interface IImdbWatchlistService
    {
        Task<IList<ImdbWatchlist>> GetWatchlistAsync(string ImdbUserId);
    }

    public class ImdbWatchlistService : IImdbWatchlistService
    {
        private readonly ILogger<ImdbWatchlistService> logger;
        private readonly IHttpClientFactory httpClientFactory;

        public ImdbWatchlistService(
            ILogger<ImdbWatchlistService> logger,
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
            var client = httpClientFactory.CreateClient("imdb");
            var response = await client.GetAsync($"/user/{imdbUserId}/watchlist?sort=date_added%2Cdesc&view=detail");
            response.EnsureSuccessStatusCode();
            // Troubleshoot: Debug console: response.Content.ReadAsStringAsync().Result

            Stream stream = await response.Content.ReadAsStreamAsync();
            TextReader textReader = new StreamReader(stream);
            string text = await textReader.ReadToEndAsync();
            text = Regex.Match(text, @"IMDbReactInitialState\.push\(({.*})\);").Groups[1].Value;
            var jsonData = System.Text.Json.JsonSerializer.Deserialize<JsonData>(text);

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
}