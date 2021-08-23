using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.Logging;

namespace FxMovies.Core
{
    public interface IImdbRatingsFromWebService
    {
        Task<IList<ImdbRating>> GetRatingsAsync(string ImdbUserId, bool getAll);
        Task<IList<ImdbRating>> GetRatingsAsync(string ImdbUserId, DateTime? fromDateTime);
    }

    public class ImdbRatingsFromWebService : IImdbRatingsFromWebService
    {
        private readonly ILogger<ImdbRatingsFromWebService> logger;
        private readonly IHttpClientFactory httpClientFactory;

        public ImdbRatingsFromWebService(
            ILogger<ImdbRatingsFromWebService> logger,
            IHttpClientFactory httpClientFactory)
        {
            this.logger = logger;
            this.httpClientFactory = httpClientFactory;
        }

        public async Task<IList<ImdbRating>> GetRatingsAsync(string imdbUserId, bool getAll)
        {
            return await GetRatingsAsync(imdbUserId, getAll ? DateTime.MinValue : null);
        }

        public async Task<IList<ImdbRating>> GetRatingsAsync(string imdbUserId, DateTime? fromDateTime)
        {
            IList<ImdbRating> ratings = new List<ImdbRating>();
            string url = $"/user/{imdbUserId}/ratings?sort=date_added%2Cdesc&mode=detail";
            do
            {
                using (var htmlDocument = await FetchHtmlDocument(url))
                {
                    url = GetRatingsSinglePageAsync(htmlDocument, ratings);
                }
            } while (url != null && fromDateTime.HasValue && fromDateTime.Value < ratings.Min(r => r.Date));
            return ratings;
        }

        private async Task<IHtmlDocument> FetchHtmlDocument(string url)
        {
            var client = httpClientFactory.CreateClient("imdb");
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            // Troubleshoot: Debug console: 
            //   response.Content.ReadAsStringAsync().Result,nq 
            // ==> nq = non-quoted

            using Stream stream = await response.Content.ReadAsStreamAsync();
            HtmlParser parser = new HtmlParser();
            return await parser.ParseDocumentAsync(stream);
        }

        private string GetRatingsSinglePageAsync(IHtmlDocument document, IList<ImdbRating> ratings)
        {
            var ratingsContainer = document.QuerySelector("#ratings-container");
            if (ratingsContainer == null)
                return null;
            var elements = ratingsContainer.GetElementsByClassName("lister-item");
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

                string title = WebUtility.UrlDecode(
                    element.QuerySelector("div:nth-child(2) > h3:nth-child(2) > a:nth-child(3)").InnerHtml.Trim());

                var rating = int.Parse(child.InnerHtml);
                ratings.Add(new ImdbRating() {
                    ImdbId = tt,
                    Rating = rating,
                    Date = date,
                    Title = title
                });
            }

            string nextUrl = document.QuerySelector("a.flat-button:nth-child(3)").Attributes["href"].Value;

            return nextUrl;
        }
    }
}