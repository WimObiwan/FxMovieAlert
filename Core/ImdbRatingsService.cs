using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
    public class ImdbRating
    {
        public string ImdbId { get; set; }
        public int Rating { get; set; }
        public DateTime Date { get; set; }
    }

    public interface IImdbRatingsService
    {
        Task<IList<ImdbRating>> GetRatingsAsync(string ImdbUserId);
    }

    public class ImdbRatingsService : IImdbRatingsService
    {
        private readonly ILogger<ImdbRatingsService> logger;
        private readonly IHttpClientFactory httpClientFactory;

        public ImdbRatingsService(
            ILogger<ImdbRatingsService> logger,
            IHttpClientFactory httpClientFactory)
        {
            this.logger = logger;
            this.httpClientFactory = httpClientFactory;
        }

        public async Task<IList<ImdbRating>> GetRatingsAsync(string ImdbUserId)
        {
            var client = httpClientFactory.CreateClient("imdb");
            var response = await client.GetAsync($"/user/{ImdbUserId}/ratings");
            response.EnsureSuccessStatusCode();
            // Troubleshoot: Debug console: response.Content.ReadAsStringAsync().Result

            Stream stream = await response.Content.ReadAsStreamAsync();
            HtmlParser parser = new HtmlParser();
            IHtmlDocument document = await parser.ParseDocumentAsync(stream);

            var ratingsContainer = document.QuerySelector("#ratings-container");
            var elements = ratingsContainer.GetElementsByClassName("lister-item");
            var ratings = new List<ImdbRating>();
            foreach(var element in elements)
            {
                var child = element.QuerySelector("div:nth-child(1)");
                var tt = child.Attributes["data-tconst"].Value;
                child = element.QuerySelector("div:nth-child(2) > p:nth-child(5)");

                var dateString = Regex.Replace(
                    child.InnerHtml,
                    "Rated on (.*)", "$1");
                var date = DateTime.ParseExact(dateString, "dd MMM yyyy", CultureInfo.InvariantCulture);
                child = element.QuerySelector("div:nth-child(2) > div:nth-child(4) > div:nth-child(2) > span:nth-child(2)");

                var rating = int.Parse(child.InnerHtml);
                ratings.Add(new ImdbRating() {
                    ImdbId = tt,
                    Rating = rating,
                    Date = date
                });
            }

            return ratings;
        }
    }
}