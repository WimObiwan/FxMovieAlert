using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using FxMovies.Core.Entities;

namespace FxMovies.Core.Services;

public class VrtMaxService : IMovieEventService
{
    private static readonly Uri BaseUrl = new("https://www.vrt.be");
    private readonly IHttpClientFactory _httpClientFactory;

    public VrtMaxService(
        IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    // Only keep first MaxCount MovieEvents for performance reasons during testing (Design for Testability)
    public int MaxCount { get; set; } = 1024;

    public string ProviderName => "VrtMax";

    public string ProviderCode => "vrtmax";

    public IList<string> ChannelCodes => new List<string>() { "vrtmax" };
    public async Task<IList<MovieEvent>> GetMovieEvents()
    {
        var movieTiles = await GetAllMovieData();

        var channel = new Channel
        {
            Code = "vrtmax",
            Name = "VRT MAX",
            LogoS = "https://www.filmoptv.be/images/vrtmax.png"
        };

        // Now you can extract movie information from each tile
        var movieEvents = new List<MovieEvent>();
        foreach (var tile in movieTiles)
        {
            var objectId = tile.objectId;
            var title = tile.title;
            var link = tile.action?.link;
            var imageUrl = tile.image?.templateUrl;
            var description = tile.image?.alt;
            //var categories = tile.primaryMeta?.Select(m => m.value).ToList();
            var vodUrl = link != null ? GetFullUrl(link) : null;

            var type = 1; // 1 = movie, 2 = short movie, 3 = serie
            // if (movieDetails.tags?.Any(t => string.Compare(t.name, "kortfilm", StringComparison.InvariantCultureIgnoreCase) == 0) ?? false)
            //     type = 2;

            await Task.Delay(500);
            var (endTime, duration, year) = await GetSingleMovieDetails(link);

            // ... create MovieEvent
            movieEvents.Add(new MovieEvent
            {
                Channel = channel,
                Title = title,
                Content = description,
                PosterM = imageUrl,
                PosterS = imageUrl,
                Vod = true,
                Feed = MovieEvent.FeedType.FreeVod,
                VodLink = vodUrl,
                Type = type,
                ExternalId = link,

                EndTime = endTime ?? DateTime.Today.AddYears(1),
                Duration = duration,
                Year = year
            });
        }

        return movieEvents;
    }

    private string GetFullUrl(string url)
    {
        return new Uri(BaseUrl, url).AbsoluteUri;
    }

    private static int? ParseDurationFromStatusText(string? statusText)
    {
        if (string.IsNullOrWhiteSpace(statusText))
            return null;
        
        // Match patterns like "133 min", "133 minuten", "83 min"
        var match = System.Text.RegularExpressions.Regex.Match(statusText, @"(\d+)\s*min");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var minutes))
        {
            return minutes;
        }
        
        return null;
    }

    private async Task<(DateTime?, int?, int?)> GetSingleMovieDetails(string? programLink)
    {
        if (string.IsNullOrEmpty(programLink))
            return (null, null, null);

        var client = _httpClientFactory.CreateClient("vrtmax");
        
        try
        {
            // Use GraphQL query to fetch program page details
            var requestBody = new
            {
                operationName = "VideoProgramPage",
                query = @"query VideoProgramPage($pageId: ID!, $lazyItemCount: Int = 10, $after: ID, $before: ID) {
  page(id: $pageId) {
    ... on ProgramPage {
      objectId
      permalink
      header {
        ...pageHeaderFragment
        __typename
      }
      components {
        __typename
        ... on ContainerNavigation {
          __typename
          objectId
          items {
            objectId
            title
            components {
              __typename
              ... on PaginatedTileList {
                ...paginatedTileListFragment
                __typename
              }
              ... on StaticTileList {
                ...staticTileListFragment
                __typename
              }
            }
            __typename
          }
        }
        ...paginatedTileListFragment
      }
      __typename
    }
    __typename
  }
}

fragment metaFragment on MetaDataItem {
  __typename
  type
  value
  shortValue
  longValue
}

fragment staticTileListFragment on StaticTileList {
  __typename
  objectId
  listId
  title
  items {
    ...tileFragment
    __typename
  }
}

fragment imageFragment on Image {
  __typename
  objectId
  alt
  focusPoint {
    x
    y
    __typename
  }
  templateUrl
}

fragment tileFragment on Tile {
  ... on IIdentifiable {
    __typename
    objectId
  }
  ... on ITile {
    title
    tileType
    image {
      ...imageFragment
      __typename
    }
    primaryMeta {
      ...metaFragment
      __typename
    }
    secondaryMeta {
      ...metaFragment
      __typename
    }
    status {
      accessibilityLabel
      text {
        small
        default
        __typename
      }
      __typename
    }
    __typename
  }
  ... on EpisodeTile {
    description
    available
    progress {
      durationInSeconds
      __typename
    }
    __typename
  }
  __typename
}

fragment paginatedTileListFragment on PaginatedTileList {
  __typename
  objectId
  listId
  paginatedItems(first: $lazyItemCount, after: $after, before: $before) {
    __typename
    edges {
      __typename
      node {
        __typename
        ...tileFragment
      }
    }
    pageInfo {
      __typename
      endCursor
      hasNextPage
      startCursor
    }
  }
  title
  __typename
}

fragment pageHeaderFragment on PageHeader {
  objectId
  title
  secondaryMeta {
    type
    value
    shortValue
    longValue
    __typename
  }
  __typename
}",
                variables = new
                {
                    pageId = programLink,
                    lazyItemCount = 10
                }
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("/vrtnu-api/graphql/public/v1", content);
            response.EnsureSuccessStatusCode();

            var programPageResponse = await response.Content.ReadFromJsonAsync<ProgramPageResponse>();
            if (programPageResponse?.data?.page == null)
                return (null, null, null);

            var programPage = programPageResponse.data.page;

            // Extract year from secondaryMeta
            int? year = null;
            var yearMeta = programPage.header?.secondaryMeta?
                .FirstOrDefault(m => m.type == "default" && m.value != null && 
                    int.TryParse(m.value, out var y) && y >= 1930 && y <= DateTime.Now.Year);
            if (yearMeta != null && int.TryParse(yearMeta.value, out var parsedYear))
            {
                year = parsedYear;
            }

            // Extract duration from episode tiles
            int? duration = null;
            
            // Collect tiles from components (including nested ones in ContainerNavigation)
            var allTiles = new List<ProgramTile>();
            
            void CollectTilesFromComponent(ProgramComponent component)
            {
                // StaticTileList has items directly
                var tileItems = component.GetTileItems();
                if (tileItems != null)
                {
                    allTiles.AddRange(tileItems.Where(t => t != null)!);
                }
                
                // PaginatedTileList has paginatedItems.edges[].node
                if (component.paginatedItems?.edges != null)
                {
                    foreach (var edge in component.paginatedItems.edges)
                    {
                        if (edge?.node != null)
                        {
                            allTiles.Add(edge.node);
                        }
                    }
                }
                
                // ContainerNavigation has items with nested components
                var navItems = component.GetNavigationItems();
                if (navItems != null)
                {
                    foreach (var navItem in navItems)
                    {
                        if (navItem?.components != null)
                        {
                            foreach (var nestedComponent in navItem.components)
                            {
                                CollectTilesFromComponent(nestedComponent);
                            }
                        }
                    }
                }
            }
            
            if (programPage.components != null)
            {
                foreach (var component in programPage.components)
                {
                    CollectTilesFromComponent(component);
                }
            }
            
            var episodeTile = allTiles.FirstOrDefault(t => t.__typename == "EpisodeTile");

            if (episodeTile?.progress?.durationInSeconds != null)
            {
                duration = (int)(episodeTile.progress.durationInSeconds / 60);
            }
            
            // If duration not found in progress, try parsing from status.text (e.g., "133 min")
            if (duration == null && episodeTile?.status?.text != null)
            {
                duration = ParseDurationFromStatusText(episodeTile.status.text.small) 
                    ?? ParseDurationFromStatusText(episodeTile.status.text.defaultText);
            }

            // For endTime, we would need announcement data which isn't in this GraphQL query
            // We'll return a default value for now
            DateTime? endTime = DateTime.Today.AddYears(1);

            return (endTime, duration, year);
        }
        catch (HttpRequestException)
        {
            // Failed to fetch details, return nulls
            return (null, null, null);
        }
    }

    private async Task<IList<Tile>> GetAllMovieData()
    {
        var client = _httpClientFactory.CreateClient("vrtmax");

        // Prepare the request body
        var requestBody = new
        {
            operationName = "Page",
            query = "query Page($pageId: ID!, $lazyItemCount: Int = 10, $after: ID, $before: ID, $componentCount: Int = 5, $componentAfter: ID) {\n  page(id: $pageId) {\n    ... on IIdentifiable {\n      __typename\n      objectId\n    }\n    ... on IPage {\n      id\n      permalink\n      title\n      ldjson\n      header {\n        title\n        primaryMeta {\n          longValue\n          shortValue\n          type\n          value\n          __typename\n        }\n        __typename\n      }\n      popUp: nudge {\n        ...popupFragment\n        __typename\n      }\n      toast: nudge {\n        ...toastFragment\n        __typename\n      }\n      ...paginatedComponents\n      seo {\n        ...seoFragment\n        __typename\n      }\n      socialSharing {\n        ...socialSharingFragment\n        __typename\n      }\n      trackingData {\n        ...trackingDataFragment\n        __typename\n      }\n      heading {\n        ... on ImageBillboard {\n          ...ImageBillboardFragment\n          __typename\n        }\n        ... on Banner {\n          ...bannerFragment\n          __typename\n        }\n        __typename\n      }\n      __typename\n    }\n    ... on ArticlePage {\n      publicationDate {\n        __typename\n        raw\n        formatted\n      }\n      __typename\n    }\n    ...errorFragment\n    __typename\n  }\n}\nfragment paginatedComponents on IPage {\n  paginatedComponents(first: $componentCount, after: $componentAfter) {\n    __typename\n    pageInfo {\n      hasNextPage\n      startCursor\n      endCursor\n      __typename\n    }\n    edges {\n      __typename\n      node {\n        ... on IIdentifiable {\n          __typename\n          objectId\n        }\n        ... on IComponent {\n          ...componentTrackingDataFragment\n          __typename\n        }\n        ... on PageHeader {\n          ...pageHeaderFragment\n          __typename\n        }\n        ... on Banner {\n          ...bannerFragment\n          __typename\n        }\n        ... on PaginatedTileList {\n          ...paginatedTileListFragment\n          __typename\n        }\n        ... on StaticTileList {\n          ...staticTileListFragment\n          __typename\n        }\n        ... on Text {\n          ...textFragment\n          __typename\n        }\n        ... on ResponsiveImage {\n          __typename\n          objectId\n          templateUrl\n          alt\n          focusPoint {\n            x\n            y\n            __typename\n          }\n          ... on IComponent {\n            title\n            __typename\n          }\n        }\n        ... on Quote {\n          __typename\n          objectId\n          text\n          authors\n        }\n        ... on NoContent {\n          ...noContentFragment\n          __typename\n        }\n        ...buttonFragment\n        ...containerNavigationFragment\n        __typename\n      }\n    }\n  }\n  __typename\n}\nfragment staticTileListFragment on StaticTileList {\n  __typename\n  objectId\n  listId\n  title\n  description\n  tileContentType\n  displayType\n  maxAge\n  tileVariant\n  action {\n    ... on LinkAction {\n      __typename\n      externalTarget\n      link\n    }\n    ... on SwitchTabAction {\n      __typename\n      link\n      referencedTabId\n    }\n    __typename\n  }\n  banner {\n    actionItems {\n      ...actionItemFragment\n      __typename\n    }\n    description\n    image {\n      ...imageFragment\n      __typename\n    }\n    compactLayout\n    backgroundColor\n    textTheme\n    title\n    titleArt {\n      objectId\n      templateUrl\n      __typename\n    }\n    __typename\n  }\n  bannerSize\n  items {\n    ...tileFragment\n    __typename\n  }\n  ... on IComponent {\n    ...componentTrackingDataFragment\n    __typename\n  }\n}\nfragment actionItemFragment on ActionItem {\n  __typename\n  objectId\n  accessibilityLabel\n  active\n  mode\n  title\n  themeOverride\n  action {\n    ...actionFragment\n    __typename\n  }\n  icons {\n    ...iconFragment\n    __typename\n  }\n}\nfragment actionFragment on Action {\n  __typename\n  ... on FavoriteAction {\n    id\n    favorite\n    title\n    __typename\n  }\n  ... on ListDeleteAction {\n    listName\n    id\n    listId\n    title\n    __typename\n  }\n  ... on ListTileDeletedAction {\n    listName\n    id\n    listId\n    __typename\n  }\n  ... on LinkAction {\n    internalTarget\n    link\n    internalTarget\n    externalTarget\n    passUserIdentity\n    zone {\n      preferredZone\n      isExclusive\n      __typename\n    }\n    linkTokens {\n      __typename\n      placeholder\n      value\n    }\n    __typename\n  }\n  ... on ClientDrivenAction {\n    __typename\n    clientDrivenActionType\n  }\n  ... on ShareAction {\n    title\n    url\n    __typename\n  }\n  ... on SwitchTabAction {\n    referencedTabId\n    link\n    __typename\n  }\n  ... on FinishAction {\n    id\n    __typename\n  }\n}\nfragment iconFragment on Icon {\n  __typename\n  accessibilityLabel\n  position\n  type\n  ... on DesignSystemIcon {\n    value {\n      __typename\n      color\n      name\n    }\n    activeValue {\n      __typename\n      color\n      name\n    }\n    __typename\n  }\n  ... on ImageIcon {\n    value {\n      __typename\n      srcSet {\n        src\n        format\n        __typename\n      }\n    }\n    activeValue {\n      __typename\n      srcSet {\n        src\n        format\n        __typename\n      }\n    }\n    __typename\n  }\n}\nfragment componentTrackingDataFragment on IComponent {\n  trackingData {\n    data\n    perTrigger {\n      trigger\n      data\n      template {\n        id\n        __typename\n      }\n      __typename\n    }\n    __typename\n  }\n  __typename\n}\nfragment imageFragment on Image {\n  __typename\n  objectId\n  alt\n  focusPoint {\n    x\n    y\n    __typename\n  }\n  templateUrl\n}\nfragment tileFragment on Tile {\n  ... on IIdentifiable {\n    __typename\n    objectId\n  }\n  ... on IComponent {\n    ...componentTrackingDataFragment\n    __typename\n  }\n  ... on ITile {\n    title\n    active\n    accessibilityTitle\n    tileType\n    action {\n      __typename\n      ... on LinkAction {\n        internalTarget\n        link\n        internalTarget\n        externalTarget\n        __typename\n      }\n    }\n    actionItems {\n      ...actionItemFragment\n      __typename\n    }\n    image {\n      ...imageFragment\n      __typename\n    }\n    primaryMeta {\n      ...metaFragment\n      __typename\n    }\n    secondaryMeta {\n      ...metaFragment\n      __typename\n    }\n    tertiaryMeta {\n      ...metaFragment\n      __typename\n    }\n    indexMeta {\n      __typename\n      type\n      value\n    }\n    status {\n      accessibilityLabel\n      icon {\n        ...iconFragment\n        __typename\n      }\n      text {\n        small\n        default\n        __typename\n      }\n      __typename\n    }\n    labelMeta {\n      __typename\n      type\n      value\n    }\n    __typename\n  }\n  ... on ContentTile {\n    brand\n    brandLogos {\n      ...brandLogosFragment\n      __typename\n    }\n    __typename\n  }\n  ... on BannerTile {\n    backgroundColor\n    brand\n    brandLogos {\n      ...brandLogosFragment\n      __typename\n    }\n    compactLayout\n    description\n    textTheme\n    titleArt {\n      objectId\n      templateUrl\n      __typename\n    }\n    __typename\n  }\n  ... on EpisodeTile {\n    description\n    available\n    chapterStart\n    progress {\n      completed\n      progressInSeconds\n      durationInSeconds\n      __typename\n    }\n    __typename\n  }\n  ... on PodcastEpisodeTile {\n    available\n    description\n    progress {\n      completed\n      progressInSeconds\n      durationInSeconds\n      __typename\n    }\n    __typename\n  }\n  ... on AudioLivestreamTile {\n    brand\n    brandsLogos {\n      brand\n      brandTitle\n      logos {\n        ...brandLogosFragment\n        __typename\n      }\n      __typename\n    }\n    description\n    progress {\n      durationInSeconds\n      progressInSeconds\n      __typename\n    }\n    __typename\n  }\n  ... on LivestreamTile {\n    description\n    progress {\n      durationInSeconds\n      progressInSeconds\n      __typename\n    }\n    __typename\n  }\n  ... on ButtonTile {\n    mode\n    icons {\n      ...iconFragment\n      __typename\n    }\n    __typename\n  }\n  ... on RadioEpisodeTile {\n    available\n    description\n    progress {\n      completed\n      progressInSeconds\n      durationInSeconds\n      __typename\n    }\n    __typename\n  }\n  ... on RadioFragmentTile {\n    progress {\n      completed\n      progressInSeconds\n      durationInSeconds\n      __typename\n    }\n    __typename\n  }\n  ... on SongTile {\n    startDate\n    formattedStartDate\n    endDate\n    description\n    __typename\n  }\n  __typename\n}\nfragment brandLogosFragment on Logo {\n  colorOnColor\n  height\n  mono\n  primary\n  type\n  width\n  __typename\n}\nfragment metaFragment on MetaDataItem {\n  __typename\n  type\n  value\n  shortValue\n  longValue\n}\nfragment paginatedTileListFragment on PaginatedTileList {\n  __typename\n  objectId\n  listId\n  action {\n    ... on LinkAction {\n      __typename\n      externalTarget\n      link\n    }\n    ... on SwitchTabAction {\n      __typename\n      link\n      referencedTabId\n    }\n    __typename\n  }\n  banner {\n    actionItems {\n      ...actionItemFragment\n      __typename\n    }\n    backgroundColor\n    compactLayout\n    description\n    image {\n      ...imageFragment\n      __typename\n    }\n    titleArt {\n      ...imageFragment\n      __typename\n    }\n    textTheme\n    title\n    __typename\n  }\n  bannerSize\n  displayType\n  maxAge\n  tileVariant\n  paginatedItems(first: $lazyItemCount, after: $after, before: $before) {\n    __typename\n    edges {\n      __typename\n      cursor\n      node {\n        __typename\n        ...tileFragment\n      }\n    }\n    pageInfo {\n      __typename\n      endCursor\n      hasNextPage\n      hasPreviousPage\n      startCursor\n    }\n  }\n  tileContentType\n  title\n  description\n  ... on IComponent {\n    ...componentTrackingDataFragment\n    __typename\n  }\n}\nfragment pageHeaderFragment on PageHeader {\n  objectId\n  accessibilityTitle\n  brandsLogos {\n    brandTitle\n    __typename\n  }\n  presenters {\n    title\n    __typename\n  }\n  title\n  titleArt {\n    objectId\n    templateUrl\n    __typename\n  }\n  richDescription {\n    __typename\n    html\n  }\n  actionItems {\n    ...actionItemFragment\n    __typename\n  }\n  primaryMeta {\n    type\n    value\n    shortValue\n    __typename\n  }\n  secondaryMeta {\n    type\n    value\n    shortValue\n    longValue\n    __typename\n  }\n  tertiaryMeta {\n    type\n    value\n    shortValue\n    __typename\n  }\n  image {\n    __typename\n    objectId\n    focusPoint {\n      x\n      y\n      __typename\n    }\n    templateUrl\n  }\n  __typename\n}\nfragment bannerFragment on Banner {\n  __typename\n  objectId\n  accessibilityTitle\n  brand\n  countdown {\n    date\n    __typename\n  }\n  richDescription {\n    __typename\n    text\n  }\n  image {\n    objectId\n    templateUrl\n    alt\n    focusPoint {\n      x\n      y\n      __typename\n    }\n    __typename\n  }\n  title\n  compactLayout\n  textTheme\n  backgroundColor\n  style\n  action {\n    ...actionFragment\n    __typename\n  }\n  actionItems {\n    ...actionItemFragment\n    __typename\n  }\n  titleArt {\n    objectId\n    templateUrl\n    __typename\n  }\n  labelMeta {\n    __typename\n    type\n    value\n  }\n  preview {\n    video {\n      objectId\n      modes {\n        __typename\n        streamId\n      }\n      __typename\n    }\n    __typename\n  }\n  ... on IComponent {\n    ...componentTrackingDataFragment\n    __typename\n  }\n}\nfragment textFragment on Text {\n  __typename\n  objectId\n  html\n}\nfragment buttonFragment on Button {\n  __typename\n  objectId\n  title\n  accessibilityTitle\n  mode\n  action {\n    ...actionFragment\n    __typename\n  }\n  ...componentTrackingDataFragment\n}\nfragment noContentFragment on NoContent {\n  __typename\n  objectId\n  title\n  text\n  backgroundImage {\n    ...imageFragment\n    __typename\n  }\n  mainImage {\n    ...imageFragment\n    __typename\n  }\n  noContentType\n  actionItems {\n    ...actionItemFragment\n    __typename\n  }\n}\nfragment containerNavigationFragment on ContainerNavigation {\n  __typename\n  objectId\n  navigationType\n  items {\n    __typename\n    objectId\n    componentId\n    active\n    action {\n      ... on SwitchTabAction {\n        __typename\n        referencedTabId\n        link\n      }\n      __typename\n    }\n    title\n    total\n    mediaType\n    disabled\n  }\n}\nfragment ImageBillboardFragment on ImageBillboard {\n  __typename\n  objectId\n  size\n  background {\n    ...imageFragment\n    __typename\n  }\n  midground {\n    ...imageFragment\n    __typename\n  }\n  titleArt {\n    ...imageFragment\n    __typename\n  }\n}\nfragment seoFragment on SeoProperties {\n  __typename\n  title\n  description\n}\nfragment socialSharingFragment on SocialSharingProperties {\n  __typename\n  title\n  description\n  image {\n    __typename\n    objectId\n    templateUrl\n  }\n}\nfragment trackingDataFragment on PageTrackingData {\n  data\n  perTrigger {\n    trigger\n    data\n    template {\n      id\n      __typename\n    }\n    __typename\n  }\n  __typename\n}\nfragment errorFragment on ErrorPage {\n  errorComponents: components {\n    ...noContentFragment\n    __typename\n  }\n  __typename\n}\nfragment popupFragment on PopUp {\n  __typename\n  brand\n  brandsLogos {\n    ...brandLogo\n    __typename\n  }\n  buttons {\n    ...actionItemFragment\n    __typename\n  }\n  description\n  image {\n    ...imageFragment\n    __typename\n  }\n  objectId\n  size\n  title\n  trackingData {\n    ...trackingDataFragment\n    __typename\n  }\n}\nfragment brandLogo on BrandLogo {\n  brand\n  brandTitle\n  logos {\n    type\n    primary\n    colorOnColor\n    __typename\n  }\n  __typename\n}\nfragment toastFragment on Toast {\n  __typename\n  description\n  image {\n    ...imageFragment\n    __typename\n  }\n  objectId\n  title\n}",
            variables = new
            {
                componentAfter = "",
                componentCount = 4,
                pageId = "/vrtmax/films/"
            }
        };

        var jsonContent = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/vrtnu-api/graphql/public/v1", content);
        response.EnsureSuccessStatusCode();

        // var responseBody = await response.Content.ReadAsStringAsync();
        // // Or put this in the Debug Console: responseBody,nq
        // Console.WriteLine(responseBody);

        var responseObject = await response.Content.ReadFromJsonAsync<GraphQLPageResponse>() 
            ?? throw new Exception("Response is missing");

        var page = responseObject.data?.page;
        var components = page?.paginatedComponents?.edges?.Select(e => e.node) ?? Enumerable.Empty<ComponentNode>();

        // Extract all tiles from all components
        var allTiles = new List<Tile>();

        foreach (var component in components)
        {
            if (component == null)
                continue;

            if (component.__typename == "PaginatedTileList")
            {
                // Get tiles from paginated items
                var tiles = component.paginatedItems?.edges?
                    .Select(e => e.node)
                    .Where(t => t != null)
                    .Select(t => t!);
                
                if (tiles != null)
                    allTiles.AddRange(tiles);
            }
            else if (component.__typename == "StaticTileList")
            {
                // Get tiles directly from items
                if (component.items != null)
                    allTiles.AddRange(component.items);
            }
        }

        // Filter for movie tiles only (optional)
        var movieTiles = allTiles
            .GroupBy(t => t.objectId) // Remove duplicates
            .Select(g => g.First())
            // .Where(t => t.tileType == "program")
            .ToList();

        return movieTiles;
    }

    #region GraphQL Response Model for GetAllMovieData

    // ReSharper disable All

    public class GraphQLPageResponse
    {
        public PageData? data { get; set; }
    }

    public class PageData
    {
        public Page? page { get; set; }
    }

    public class Page
    {
        public string? __typename { get; set; }
        public string? objectId { get; set; }
        public string? id { get; set; }
        public string? permalink { get; set; }
        public string? title { get; set; }
        public string[]? ldjson { get; set; }
        public PageHeader? header { get; set; }
        public object? popUp { get; set; }
        public object? toast { get; set; }
        public PaginatedComponents? paginatedComponents { get; set; }
        public SeoProperties? seo { get; set; }
        public SocialSharingProperties? socialSharing { get; set; }
        public PageTrackingData? trackingData { get; set; }
        public Heading? heading { get; set; }
    }

    public class PageHeader
    {
        public string? title { get; set; }
        public MetaDataItem[]? primaryMeta { get; set; }
        public string? __typename { get; set; }
    }

    public class PaginatedComponents
    {
        public string? __typename { get; set; }
        public PageInfo? pageInfo { get; set; }
        public ComponentEdge[]? edges { get; set; }
    }

    public class ComponentEdge
    {
        public string? __typename { get; set; }
        public ComponentNode? node { get; set; }
    }

    public class ComponentNode
    {
        public string? __typename { get; set; }
        public string? objectId { get; set; }
        public PageTrackingData? trackingData { get; set; }
        
        // Banner properties
        public string? accessibilityTitle { get; set; }
        public string? brand { get; set; }
        public object? countdown { get; set; }
        public RichText? richDescription { get; set; }
        public Image? image { get; set; }
        public string? title { get; set; }
        public bool? compactLayout { get; set; }
        public string? textTheme { get; set; }
        public string? backgroundColor { get; set; }
        public string? style { get; set; }
        public Action? action { get; set; }
        public ActionItem[]? actionItems { get; set; }
        public Image? titleArt { get; set; }
        public MetaDataItem[]? labelMeta { get; set; }
        public ContentPreview? preview { get; set; }
        
        // PaginatedTileList / StaticTileList properties
        public string? listId { get; set; }
        public Banner? banner { get; set; }
        public string? bannerSize { get; set; }
        public string? displayType { get; set; }
        public int? maxAge { get; set; }
        public string? tileVariant { get; set; }
        public TileConnection? paginatedItems { get; set; }
        public string? tileContentType { get; set; }
        public string? description { get; set; }
        public Tile[]? items { get; set; }
    }

    public class TileConnection
    {
        public string? __typename { get; set; }
        public TileEdge[]? edges { get; set; }
        public PageInfo? pageInfo { get; set; }
    }

    public class TileEdge
    {
        public string? __typename { get; set; }
        public string? cursor { get; set; }
        public Tile? node { get; set; }
    }

    public class Tile
    {
        public string? __typename { get; set; }
        public string? objectId { get; set; }
        public PageTrackingData? trackingData { get; set; }
        public string? title { get; set; }
        public bool? active { get; set; }
        public string? accessibilityTitle { get; set; }
        public string? tileType { get; set; }
        public Action? action { get; set; }
        public ActionItem[]? actionItems { get; set; }
        public Image? image { get; set; }
        public MetaDataItem[]? primaryMeta { get; set; }
        public MetaDataItem[]? secondaryMeta { get; set; }
        public MetaDataItem[]? tertiaryMeta { get; set; }
        public MetaDataItem? indexMeta { get; set; }
        public Status? status { get; set; }
        public MetaDataItem? labelMeta { get; set; }
        public string? brand { get; set; }
        public string? description { get; set; }
    }

    public class Banner
    {
        public ActionItem[]? actionItems { get; set; }
        public string? backgroundColor { get; set; }
        public bool? compactLayout { get; set; }
        public string? description { get; set; }
        public Image? image { get; set; }
        public Image? titleArt { get; set; }
        public string? textTheme { get; set; }
        public string? title { get; set; }
        public string? __typename { get; set; }
    }

    public class Action
    {
        public string? __typename { get; set; }
        public string? internalTarget { get; set; }
        public string? link { get; set; }
        public string? externalTarget { get; set; }
        public bool? passUserIdentity { get; set; }
        public object? zone { get; set; }
        public object[]? linkTokens { get; set; }
        public string? title { get; set; }
        public string? url { get; set; }
    }

    public class ActionItem
    {
        public string? __typename { get; set; }
        public string? objectId { get; set; }
        public string? accessibilityLabel { get; set; }
        public bool? active { get; set; }
        public string? mode { get; set; }
        public string? title { get; set; }
        public string? themeOverride { get; set; }
        public Action? action { get; set; }
        public Icon[]? icons { get; set; }
    }

    public class Icon
    {
        public string? __typename { get; set; }
        public string? accessibilityLabel { get; set; }
        public string? position { get; set; }
        public string? type { get; set; }
        public DesignSystemIconValue? value { get; set; }
        public DesignSystemIconValue? activeValue { get; set; }
    }

    public class DesignSystemIconValue
    {
        public string? __typename { get; set; }
        public string? color { get; set; }
        public string? name { get; set; }
    }

    public class Image
    {
        public string? __typename { get; set; }
        public string? objectId { get; set; }
        public string? alt { get; set; }
        public FocusPoint? focusPoint { get; set; }
        public string? templateUrl { get; set; }
    }

    public class FocusPoint
    {
        public int? x { get; set; }
        public int? y { get; set; }
        public string? __typename { get; set; }
    }

    public class MetaDataItem
    {
        public string? __typename { get; set; }
        public string? type { get; set; }
        public string? value { get; set; }
        public string? shortValue { get; set; }
        public string? longValue { get; set; }
    }

    public class Status
    {
        public string? accessibilityLabel { get; set; }
        public Icon? icon { get; set; }
        public StatusText? text { get; set; }
        public string? __typename { get; set; }
    }

    public class StatusText
    {
        public string? small { get; set; }
        [JsonPropertyName("default")]
        public string? defaultText { get; set; }
        public string? __typename { get; set; }
    }

    public class RichText
    {
        public string? __typename { get; set; }
        public string? text { get; set; }
        public string? html { get; set; }
    }

    public class ContentPreview
    {
        public object? video { get; set; }
        public string? __typename { get; set; }
    }

    public class PageInfo
    {
        public bool? hasNextPage { get; set; }
        public string? startCursor { get; set; }
        public string? endCursor { get; set; }
        public bool? hasPreviousPage { get; set; }
        public string? __typename { get; set; }
    }

    public class PageTrackingData
    {
        public string? data { get; set; }
        public TriggerTrackingData[]? perTrigger { get; set; }
        public string? __typename { get; set; }
    }

    public class TriggerTrackingData
    {
        public string? trigger { get; set; }
        public string? data { get; set; }
        public TriggerTrackingDataTemplate? template { get; set; }
        public string? __typename { get; set; }
    }

    public class TriggerTrackingDataTemplate
    {
        public string? id { get; set; }
        public string? __typename { get; set; }
    }

    public class SeoProperties
    {
        public string? __typename { get; set; }
        public string? title { get; set; }
        public string? description { get; set; }
    }

    public class SocialSharingProperties
    {
        public string? __typename { get; set; }
        public string? title { get; set; }
        public string? description { get; set; }
        public Image? image { get; set; }
    }

    public class Heading
    {
        public string? __typename { get; set; }
        public string? objectId { get; set; }
        public string? size { get; set; }
        public Image? background { get; set; }
        public Image? midground { get; set; }
        public Image? titleArt { get; set; }
    }

    #endregion

    #region GraphQL Response Model for GetSingleMovieDetails

    // ReSharper disable All

    public class ProgramPageResponse
    {
        public ProgramPageData? data { get; set; }
    }

    public class ProgramPageData
    {
        public ProgramPage? page { get; set; }
    }

    public class ProgramPage
    {
        public string? __typename { get; set; }
        public string? objectId { get; set; }
        public string? permalink { get; set; }
        public ProgramPageHeader? header { get; set; }
        public ProgramComponent[]? components { get; set; }
    }

    public class ProgramPageHeader
    {
        public string? objectId { get; set; }
        public string? title { get; set; }
        public MetaDataItem[]? secondaryMeta { get; set; }
        public string? __typename { get; set; }
    }

    public class ProgramComponent
    {
        public string? __typename { get; set; }
        public string? objectId { get; set; }
        public string? listId { get; set; }
        public string? title { get; set; }
        
        // For StaticTileList - items are tiles
        [JsonPropertyName("items")]
        public System.Text.Json.JsonElement? itemsRaw { get; set; }
        
        public ProgramTileConnection? paginatedItems { get; set; }
        
        // Helper to get tiles from StaticTileList items
        public ProgramTile[]? GetTileItems()
        {
            if (itemsRaw == null || __typename != "StaticTileList")
                return null;
            
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<ProgramTile[]>(itemsRaw.Value.GetRawText());
            }
            catch
            {
                return null;
            }
        }
        
        // Helper to get navigation items from ContainerNavigation items
        public ContainerNavigationItem[]? GetNavigationItems()
        {
            if (itemsRaw == null || __typename != "ContainerNavigation")
                return null;
            
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<ContainerNavigationItem[]>(itemsRaw.Value.GetRawText());
            }
            catch
            {
                return null;
            }
        }
    }

    public class ContainerNavigationItem
    {
        public string? objectId { get; set; }
        public string? title { get; set; }
        public ProgramComponent[]? components { get; set; }
        public string? __typename { get; set; }
    }

    public class ProgramTileConnection
    {
        public string? __typename { get; set; }
        public ProgramTileEdge[]? edges { get; set; }
        public PageInfo? pageInfo { get; set; }
    }

    public class ProgramTileEdge
    {
        public string? __typename { get; set; }
        public ProgramTile? node { get; set; }
    }

    public class ProgramTile
    {
        public string? __typename { get; set; }
        public string? objectId { get; set; }
        public string? title { get; set; }
        public string? tileType { get; set; }
        public string? description { get; set; }
        public bool? available { get; set; }
        public Image? image { get; set; }
        public MetaDataItem[]? primaryMeta { get; set; }
        public MetaDataItem[]? secondaryMeta { get; set; }
        public ProgramTileProgress? progress { get; set; }
        public StatusIndicator? status { get; set; }
    }

    public class StatusIndicator
    {
        public string? accessibilityLabel { get; set; }
        public ResponsiveText? text { get; set; }
        public string? __typename { get; set; }
    }

    public class ResponsiveText
    {
        public string? small { get; set; }
        [JsonPropertyName("default")]
        public string? defaultText { get; set; }
        public string? __typename { get; set; }
    }

    public class ProgramTileProgress
    {
        public int? durationInSeconds { get; set; }
        public string? __typename { get; set; }
    }

    #endregion
}
