using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FxMovies.Core.Entities;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FxMovies.Core.Services;

public class VtmGoServiceOptions
{
    public static string Position => "VtmGoService";

    // https://vtm.be/vtmgo --> login --> F12 --> Tab "Storage" --> Cookies --> https://vtm.be --> lfvp_auth_token --> "ey...
    public string AuthToken { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
}

public class VtmGoService : IMovieEventService
{
    private readonly string _authToken;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<VtmGoService> _logger;
    private readonly string _password;
    private readonly string _username;

    public VtmGoService(
        ILogger<VtmGoService> logger,
        IOptions<VtmGoServiceOptions> vtmGoServiceOptions,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        var options = vtmGoServiceOptions.Value;
        _authToken = options.AuthToken;
        _username = options.Username;
        _password = options.Password;
        _httpClientFactory = httpClientFactory;
    }

    public string ProviderName => "VtmGo";

    public string ChannelCode => "vtmgo";

    public async Task<IList<MovieEvent>> GetMovieEvents()
    {
        // https://mitmproxy.org/
        // https://github.com/timrijckaert/vrtnu-vtmgo-goplay-service/tree/master/vtmgo/src/main/java/be/tapped/vtmgo/content
        // https://github.com/add-ons/plugin.video.vtm.go/blob/master/resources/lib/vtmgo/vtmgo.py

        string lfvpToken;
        if (string.IsNullOrEmpty(_authToken))
        {
            // why?
            await VtmGoAuthorize();

            var idToken = await VtmGoLogin();
            lfvpToken = await DpgLogin(idToken);
        }
        else
        {
            var jwtToken = new JwtSecurityToken(_authToken);
            if (DateTime.UtcNow < jwtToken.ValidTo)
            {
                _logger.LogInformation("Configured refresh token is still valid ({JwtTokenValidTo})", jwtToken.ValidTo);
                lfvpToken = _authToken;
            }
            else
            {
                _logger.LogInformation("Configured refresh token is no longer valid ({JwtTokenValidTo})",
                    jwtToken.ValidTo);
                lfvpToken = await DpgRefreshToken(_authToken);
            }
        }

        var profileId = await GetProfileId(lfvpToken);
        var movieIds = await GetCatalog(lfvpToken, profileId);

        var channel = new Channel
        {
            Code = "vtmgo",
            Name = "VTM GO",
            LogoS = "https://www.filmoptv.be/images/vtmgo.png"
        };

        var movieEvents = new List<MovieEvent>();
        foreach (var movieId in movieIds)
        {
            var movieInfo = await GetMovieInfo(lfvpToken, profileId, movieId);
            if (movieInfo.movie.durationSeconds < 75 * 60)
            {
                _logger.LogWarning("Skipped {Name}, duration {DurationSeconds} too small",
                    movieInfo.movie.name, movieInfo.movie.durationSeconds);
                continue;
            }

            movieEvents.Add(
                new MovieEvent
                {
                    ExternalId = movieInfo.movie.id,
                    Title = movieInfo.movie.name,
                    Year = movieInfo.movie.productionYear,
                    Content = movieInfo.movie.description,
                    PosterS = movieInfo.movie.smallPhotoUrl,
                    PosterM = movieInfo.movie.smallPhotoUrl,
                    Channel = channel,
                    Duration = (movieInfo.movie.durationSeconds + 30) / 60,
                    Vod = true,
                    Feed = MovieEvent.FeedType.FreeVod,
                    VodLink = $"https://vtm.be/vtmgo/~m{movieInfo.movie.id}",
                    Type = 1,
                    StartTime = DateTime.MinValue,
                    EndTime = DateTime.Now.Date.AddDays(movieInfo.movie.remainingDaysAvailable + 1)
                }
            );
        }

        return movieEvents;
    }

    private async Task VtmGoAuthorize()
    {
        var queryParams = new Dictionary<string, string>
        {
            { "client_id", "vtm-go-android" },
            { "response_type", "id_token" },
            { "scope", "openid email profile address phone" },
            { "nonce", "55007373265" },
            { "sdkVersion", "0.13.1" },
            { "state", "dnRtLWdvLWFuZHJvaWQ=" },
            { "redirect_uri", "https://login2.vtm.be/continue" }
        };
        var url = QueryHelpers.AddQueryString("/authorize", queryParams);
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        var client = _httpClientFactory.CreateClient("vtmgo_login");
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private async Task<string> VtmGoLogin()
    {
        var queryParams = new Dictionary<string, string>
        {
            { "client_id", "vtm-go-android" }
        };
        var url = QueryHelpers.AddQueryString("/login", queryParams);
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        var body = new List<KeyValuePair<string, string>>
        {
            new("userName", _username),
            new("password", _password)
        };
        request.Content = new FormUrlEncodedContent(body);
        var client = _httpClientFactory.CreateClient("vtmgo_login");
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(responseBody, @"window.location.href\s*=\s*'([^']+)'");
        if (!match.Success)
            throw new Exception("Expected redirect in body");

        url = match.Groups[1].Value;
        request = new HttpRequestMessage(HttpMethod.Get, url);
        response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var fragment = request.RequestUri?.Fragment;
        if (fragment == null || !fragment.StartsWith('#'))
            throw new Exception("Expected fragment in redirect URL");
        var fragmentQueryParams = QueryHelpers.ParseQuery(fragment.Substring(1));
        return fragmentQueryParams["id_token"];
    }

    private async Task<string> DpgLogin(string idToken)
    {
        var body = new
        {
            idToken
        };

        var client = _httpClientFactory.CreateClient("vtmgo_dpg");
        var response = await client.PostAsJsonAsync("/vtmgo/tokens", body);
        response.EnsureSuccessStatusCode();
        var responseObject = await response.Content.ReadFromJsonAsync<DpgTokenResponse>() ??
                             throw new Exception("Response is missing");
        return responseObject.lfvpToken;
    }

    private async Task<string> DpgRefreshToken(string oldToken)
    {
        var body = new
        {
            lfvpToken = oldToken
        };

        var client = _httpClientFactory.CreateClient("vtmgo_dpg");
        var response = await client.PostAsJsonAsync("/vtmgo/tokens/refresh", body);
        response.EnsureSuccessStatusCode();
        var responseObject = await response.Content.ReadFromJsonAsync<DpgTokenResponse>() ??
                             throw new Exception("Response is missing");
        return responseObject.lfvpToken;
    }

    private async Task<string> GetProfileId(string lfvpToken)
    {
        var client = _httpClientFactory.CreateClient("vtmgo_dpg");
        client.DefaultRequestHeaders.Add("lfvp-auth", lfvpToken);
        // var responseObject = await client.GetFromJsonAsync<DpgProfileResponse[]>("/profiles?products=VTM_GO,VTM_GO_KIDS");
        var response = await client.GetAsync("/profiles?products=VTM_GO,VTM_GO_KIDS");
        response.EnsureSuccessStatusCode();
        // Troubleshoot: Debug console: 
        //   response.Content.ReadAsStringAsync().Result,nq 
        // ==> nq = non-quoted

        var responseObject = await response.Content.ReadFromJsonAsync<DpgProfileResponse[]>() ??
                             throw new Exception("Response is missing");
        return responseObject[0].id;
    }

    private async Task<List<string>> GetCatalog(string lfvpToken, string profileId)
    {
        var client = _httpClientFactory.CreateClient("vtmgo_dpg");
        client.DefaultRequestHeaders.Add("lfvp-auth", lfvpToken);
        client.DefaultRequestHeaders.Add("x-dpp-profile", profileId);
        //var responseObject = await client.GetFromJsonAsync<DpgCatalogResponse>("/vtmgo/catalog?pageSize=2000");
        var response = await client.GetAsync("/vtmgo/catalog?pageSize=2000");
        response.EnsureSuccessStatusCode();
        // Troubleshoot: Debug console: 
        //   response.Content.ReadAsStringAsync().Result,nq 
        // ==> nq = non-quoted

        var responseObject = await response.Content.ReadFromJsonAsync<DpgCatalogResponse>() ??
                             throw new Exception("Response is missing");
        var movies = responseObject.pagedTeasers.content.Where(c => c.target.type == "MOVIE").Select(c => c.target.id)
            .ToList();
        return movies;
    }

    private async Task<DpgMovieResponse> GetMovieInfo(string lfvpToken, string profileId, string movieId)
    {
        var client = _httpClientFactory.CreateClient("vtmgo_dpg");
        client.DefaultRequestHeaders.Add("lfvp-auth", lfvpToken);
        client.DefaultRequestHeaders.Add("x-dpp-profile", profileId);
        var response = await client.GetAsync("/vtmgo/movies/" + movieId);
        response.EnsureSuccessStatusCode();
        // Troubleshoot: Debug console: 
        //   response.Content.ReadAsStringAsync().Result,nq 
        // ==> nq = non-quoted

        var responseObject = await response.Content.ReadFromJsonAsync<DpgMovieResponse>();
        return responseObject;
    }

    #region JsonModel

    // Resharper disable All

    private class DpgTokenResponse
    {
        public string lfvpToken { get; set; }
    }

    private class DpgProfileResponse
    {
        public string id { get; set; }
    }

    private class DpgCatalogResponse
    {
        public PagedTeasers pagedTeasers { get; set; }

        public class PagedTeasers
        {
            public Content[] content { get; set; }

            public class Content
            {
                public string title { get; set; }
                public string imageUrl { get; set; }
                public Target target { get; set; }

                public class Target
                {
                    public string type { get; set; }
                    public string id { get; set; }
                }
            }
        }
    }

    private class DpgMovieResponse
    {
        public Movie movie { get; set; }

        public class Movie
        {
            public string id { get; set; }
            public string name { get; set; }
            public string description { get; set; }
            public string smallPhotoUrl { get; set; }
            public int remainingDaysAvailable { get; set; }
            public int durationSeconds { get; set; }
            public int productionYear { get; set; }
        }
    }

    // Resharper restore All

    #endregion
}