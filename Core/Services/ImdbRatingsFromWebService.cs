using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FxMovies.Core.Entities;
using Microsoft.Extensions.Logging;

namespace FxMovies.Core.Services;

public interface IImdbRatingsFromWebService
{
    Task<IList<ImdbRating>> GetRatingsAsync(string imdbUserId, DateTime? fromDateTime);
}

public class ImdbRatingsFromWebService : IImdbRatingsFromWebService
{
    private static readonly Regex NextDataRegex = new(
        @"<script id=""__NEXT_DATA__"" type=""application/json"">(.+?)</script>",
        RegexOptions.Compiled);

    // Persisted query hash for PersonalizedUserData
    private const string PersonalizedUserDataHash = "7c4e0771d67f21fc27fd44fc46d49cc589225a9c5e63e51cc0b8d42f39ee99cc";

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
        var allRatings = new List<ImdbRating>();
        var page = 1;
        bool hasMore;

        do
        {
            var (titleInfos, pageHasMore) = await GetRatingsPageTitleIds(imdbUserId, page);

            if (titleInfos.Count == 0)
                break;

            // Fetch user ratings via GraphQL
            var ratings = await FetchUserRatings(imdbUserId, titleInfos);
            allRatings.AddRange(ratings);

            hasMore = pageHasMore;
            page++;

            // Check fromDateTime if specified
            if (fromDateTime.HasValue && allRatings.Count > 0)
            {
                var minDate = allRatings.Min(r => r.Date);
                if (minDate < fromDateTime.Value)
                    break;
            }

            _logger.LogInformation(
                "Retrieved {Count} ratings for user {UserId}, page {Page}, total so far: {Total}",
                ratings.Count, imdbUserId, page - 1, allRatings.Count);

        } while (hasMore);

        return allRatings;
    }

    private async Task<(List<TitleInfo> TitleInfos, bool HasMore)> GetRatingsPageTitleIds(string imdbUserId, int page)
    {
        var client = _httpClientFactory.CreateClient("imdb-web");
        var url = $"https://www.imdb.com/user/{imdbUserId}/ratings";
        if (page > 1)
            url += $"?page={page}";

        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();
        var match = NextDataRegex.Match(html);
        if (!match.Success)
            throw new InvalidOperationException("Could not find __NEXT_DATA__ in ratings page");

        var json = match.Groups[1].Value;
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var pageProps = root.GetProperty("props").GetProperty("pageProps");
        var mainColumnData = pageProps.GetProperty("mainColumnData");
        var advancedTitleSearch = mainColumnData.GetProperty("advancedTitleSearch");

        var total = advancedTitleSearch.GetProperty("total").GetInt32();
        var edges = advancedTitleSearch.GetProperty("edges");

        var titleInfos = new List<TitleInfo>();
        foreach (var edge in edges.EnumerateArray())
        {
            var title = edge.GetProperty("node").GetProperty("title");
            var imdbId = title.GetProperty("id").GetString()!;
            var titleText = title.GetProperty("titleText").GetProperty("text").GetString()!;
            titleInfos.Add(new TitleInfo(imdbId, titleText));
        }

        // Calculate if there are more pages (250 items per page)
        var itemsPerPage = 250;
        var itemsSoFar = (page - 1) * itemsPerPage + titleInfos.Count;
        var hasMore = itemsSoFar < total;

        return (titleInfos, hasMore);
    }

    private async Task<List<ImdbRating>> FetchUserRatings(string imdbUserId, List<TitleInfo> titleInfos)
    {
        var client = _httpClientFactory.CreateClient("imdb-graphql");

        var idArray = titleInfos.Select(t => t.ImdbId).ToArray();

        var requestBody = new
        {
            operationName = "PersonalizedUserData",
            variables = new
            {
                locale = "en-US",
                idArray = idArray,
                includeUserData = false,
                includeWatchedData = false,
                otherUserId = imdbUserId,
                fetchOtherUserRating = true
            },
            extensions = new
            {
                persistedQuery = new
                {
                    version = 1,
                    sha256Hash = PersonalizedUserDataHash
                }
            }
        };

        var jsonContent = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("https://api.graphql.imdb.com/", content);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseJson);

        var ratings = new List<ImdbRating>();
        var titlesArray = doc.RootElement.GetProperty("data").GetProperty("titles");

        // Create a lookup for title text by IMDb ID
        var titleLookup = titleInfos.ToDictionary(t => t.ImdbId, t => t.Title);

        foreach (var titleElement in titlesArray.EnumerateArray())
        {
            try
            {
                var imdbId = titleElement.GetProperty("id").GetString()!;
                var otherUserRating = titleElement.GetProperty("otherUserRating");

                if (otherUserRating.ValueKind == JsonValueKind.Null)
                    continue;

                var ratingValue = otherUserRating.GetProperty("value").GetInt32();
                var dateStr = otherUserRating.GetProperty("date").GetString();
                var date = DateTime.Parse(dateStr!);

                titleLookup.TryGetValue(imdbId, out var titleText);

                ratings.Add(new ImdbRating
                {
                    ImdbId = imdbId,
                    Rating = ratingValue,
                    Date = date,
                    Title = titleText ?? imdbId
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse rating for title");
            }
        }

        return ratings;
    }

    private record TitleInfo(string ImdbId, string Title);
}