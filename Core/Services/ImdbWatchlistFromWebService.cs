using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
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
    private static readonly Regex NextDataRegex = new(@"<script id=""__NEXT_DATA__"" type=""application/json"">(.+?)</script>", RegexOptions.Singleline | RegexOptions.Compiled);

    public ImdbWatchlistFromWebService(
        IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IList<ImdbWatchlist>> GetWatchlistAsync(string imdbUserId)
    {
        var allItems = new List<ImdbWatchlist>();
        int page = 1;
        bool hasNextPage = true;

        while (hasNextPage)
        {
            var (items, nextPage) = await GetWatchlistPage(imdbUserId, page);
            
            if (items.Count == 0)
                break;

            allItems.AddRange(items);
            hasNextPage = nextPage;
            page++;
        }

        return allItems;
    }

    private async Task<(List<ImdbWatchlist> items, bool hasNextPage)> GetWatchlistPage(string imdbUserId, int page)
    {
        var client = _httpClientFactory.CreateClient("imdb-web");
        
        var url = page == 1 
            ? $"/user/{imdbUserId}/watchlist/" 
            : $"/user/{imdbUserId}/watchlist/?page={page}";
            
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        
        var html = await response.Content.ReadAsStringAsync();
        
        // Extract __NEXT_DATA__ JSON from the HTML
        var match = NextDataRegex.Match(html);
        if (!match.Success)
            return (new List<ImdbWatchlist>(), false);

        var nextDataJson = match.Groups[1].Value;
        var nextData = JsonSerializer.Deserialize<NextDataResponse>(nextDataJson);
        
        var predefinedList = nextData?.props?.pageProps?.mainColumnData?.predefinedList;
        if (predefinedList == null)
            return (new List<ImdbWatchlist>(), false);

        var items = new List<ImdbWatchlist>();
        
        var titleListItemSearch = predefinedList.titleListItemSearch;
        if (titleListItemSearch?.edges != null)
        {
            foreach (var edge in titleListItemSearch.edges)
            {
                var listItem = edge?.listItem;
                if (listItem == null)
                    continue;

                var titleText = listItem.titleText?.text ?? listItem.originalTitleText?.text;
                
                items.Add(new ImdbWatchlist
                {
                    ImdbId = listItem.id ?? string.Empty,
                    Title = titleText,
                    Date = null
                });
            }
        }

        var pageInfo = titleListItemSearch?.pageInfo;
        return (items, pageInfo?.hasNextPage ?? false);
    }

    #region Response Models

    // ReSharper disable All

    // Models for __NEXT_DATA__ parsing
    private class NextDataResponse
    {
        public Props? props { get; set; }
    }

    private class Props
    {
        public PageProps? pageProps { get; set; }
    }

    private class PageProps
    {
        public MainColumnData? mainColumnData { get; set; }
    }

    private class MainColumnData
    {
        public PredefinedList? predefinedList { get; set; }
    }

    private class PredefinedList
    {
        public string? id { get; set; }
        public TitleListItemSearch? titleListItemSearch { get; set; }
    }

    private class TitleListItemSearch
    {
        public List<Edge>? edges { get; set; }
        public PageInfo? pageInfo { get; set; }
    }

    private class Edge
    {
        public ListItemData? listItem { get; set; }
    }

    private class ListItemData
    {
        public string? id { get; set; }
        public TitleText? titleText { get; set; }
        public TitleText? originalTitleText { get; set; }
    }

    private class TitleText
    {
        public string? text { get; set; }
    }

    private class PageInfo
    {
        public string? endCursor { get; set; }
        public bool hasNextPage { get; set; }
    }

    // ReSharper restore All

    #endregion
}