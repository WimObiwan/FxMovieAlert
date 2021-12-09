using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using FxMovies.Core.Entities;
using Microsoft.Extensions.Logging;

namespace FxMovies.Core.Services;

public interface IImdbRatingsFromWebService
{
    Task<IList<ImdbRating>> GetRatingsAsync(string imdbUserId, DateTime? fromDateTime);
}

public class ImdbRatingsFromWebService : IImdbRatingsFromWebService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ImdbRatingsFromWebService> _logger;

    public ImdbRatingsFromWebService(
        IHttpClientFactory httpClientFactory,
        ILogger<ImdbRatingsFromWebService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<IList<ImdbRating>> GetRatingsAsync(string imdbUserId, DateTime? fromDateTime)
    {
        IList<ImdbRating> ratings = new List<ImdbRating>();
        var url = $"/user/{imdbUserId}/ratings?sort=date_added%2Cdesc&mode=detail";
        do
        {
            using var htmlDocument = await FetchHtmlDocument(url);
            url = GetRatingsSinglePageAsync(htmlDocument, ratings);
        } while (url != null && fromDateTime.HasValue && fromDateTime.Value < ratings.Min(r => r.Date));

        return ratings;
    }

    private async Task<IHtmlDocument> FetchHtmlDocument(string url)
    {
        var client = _httpClientFactory.CreateClient("imdb");
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        // Troubleshoot: Debug console: 
        //   response.Content.ReadAsStringAsync().Result,nq 
        // ==> nq = non-quoted

        await using var stream = await response.Content.ReadAsStreamAsync();
        var parser = new HtmlParser();
        return await parser.ParseDocumentAsync(stream);
    }

    private string GetRatingsSinglePageAsync(IHtmlDocument document, IList<ImdbRating> ratings)
    {
        var ratingsContainer = document.QuerySelector("#ratings-container");
        if (ratingsContainer == null)
            return null;
        var elements = ratingsContainer.GetElementsByClassName("lister-item");
        foreach (var element in elements)
            try
            {
                var child = element.QuerySelector("div:nth-child(1)");
                var tt = child?.Attributes["data-tconst"]?.Value ?? throw new Exception("data-tconst not found");

                child = element.QuerySelector("div:nth-child(2) > p:nth-child(5)");
                var dateString = child?.InnerHtml ?? throw new Exception("date not found");
                dateString = Regex.Replace(
                    dateString,
                    "Rated on (.*)", "$1");
                var date = DateTime.ParseExact(dateString, "dd MMM yyyy", CultureInfo.InvariantCulture);

                var title = element.QuerySelector("div:nth-child(2) > h3:nth-child(2) > a:nth-child(3)")
                    ?.InnerHtml.Trim() ?? throw new Exception("title not found");
                title = WebUtility.UrlDecode(title);

                child = element.QuerySelector(
                    "div:nth-child(2) > div:nth-child(4) > div:nth-child(2) > span:nth-child(2)");
                var ratingString = child?.InnerHtml ?? throw new Exception("rating not found");
                var rating = int.Parse(ratingString);
                ratings.Add(new ImdbRating
                {
                    ImdbId = tt,
                    Rating = rating,
                    Date = date,
                    Title = title
                });
            }
            catch (Exception x)
            {
                _logger.LogWarning(x, "Skipping element");
            }

        return document.QuerySelector("a.flat-button:nth-child(3)")?.Attributes["href"]?.Value;
    }
}