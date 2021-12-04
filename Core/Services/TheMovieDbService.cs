using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FxMovies.Core.Services;

public class GetImagesResult
{
    public string Medium { get; set; }
    public string Small { get; set; }
}

public interface ITheMovieDbService
{
    Task<string> GetCertification(string imdbId);
    Task<GetImagesResult> GetImages(string imdbId);
}

public class TheMovieDbServiceOptions
{
    public static string Position => "TheMovieDbService";

    public string ApiKey { get; set; }
    public string[] CertificationCountryPreference { get; set; }
}

public class TheMovieDbService : ITheMovieDbService
{
    #region JSonModel

    [DebuggerDisplay("iso_3166_1 = {iso_3166_1}")]
    private class Country
    {
        public string certification { get; set; }
        public string iso_3166_1 { get; set; }
    }

    private class Releases
    {
        public List<Country> countries { get; set; }
    }

    private class Movie
    {
        public string backdrop_path { get; set; }
        public string poster_path { get; set; }
        public string original_title { get; set; }
    }

    #endregion

    private readonly ILogger<TheMovieDbServiceOptions> logger;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly string apiKey;
    private readonly string[] certificationCountryPreference;

    public TheMovieDbService(ILogger<TheMovieDbServiceOptions> logger,
        IOptionsSnapshot<TheMovieDbServiceOptions> options,
        IHttpClientFactory httpClientFactory)
    {
        this.logger = logger;
        this.httpClientFactory = httpClientFactory;
        var o = options.Value;
        apiKey = o.ApiKey;
        certificationCountryPreference = o.CertificationCountryPreference;
    }

    public async Task<string> GetCertification(string imdbId)
    {
        if (string.IsNullOrEmpty(apiKey) || certificationCountryPreference == null)
            return "";

        var queryParams = new Dictionary<string, string>();
        queryParams.Add("api_key", apiKey);
        queryParams.Add("language", "en-US");
        queryParams.Add("append_to_response", "releases");
        var url = QueryHelpers.AddQueryString($"/3/movie/{imdbId}", queryParams);
        var client = httpClientFactory.CreateClient("tmdb");

        try
        {
            var releases = await client.GetFromJsonAsync<Releases>(url);
            var certifications = releases?.countries;
            if (certifications == null)
            {
                logger.LogInformation("Certification {ImdbId} ==> NONE", imdbId);
                return null;
            }

            foreach (var countryId in certificationCountryPreference)
            {
                var certification = certifications.FirstOrDefault(c =>
                    c.iso_3166_1 == countryId && !string.IsNullOrEmpty(c.certification));
                if (certification != null)
                {
                    var label = $"{certification.iso_3166_1}:{certification.certification}";
                    logger.LogInformation("Certification {ImdbId} ==> {Label}", imdbId, label);
                    return label;
                }
            }

            logger.LogInformation("Certification {ImdbId} ==> NOT FOUND IN {CertificationsCount} items", imdbId,
                certifications.Count);
        }
        catch (Exception x)
        {
            logger.LogError(x, "Certification {ImdbId} ==> EXCEPTION", imdbId);
        }

        return null;
    }

    public async Task<GetImagesResult> GetImages(string imdbId)
    {
        // https://api.themoviedb.org/3/movie/tt0114436?api_key=<api_key>&language=en-US

        var queryParams = new Dictionary<string, string>();
        queryParams.Add("api_key", apiKey);
        queryParams.Add("language", "en-US");
        var url = QueryHelpers.AddQueryString($"/3/movie/{imdbId}", queryParams);
        var client = httpClientFactory.CreateClient("tmdb");

        try
        {
            var movie = await client.GetFromJsonAsync<Movie>(url);

            logger.LogInformation("Image {ImdbId} ==> {OriginalTitle}", imdbId, movie.original_title);

            var baseUrl = "http://image.tmdb.org/t/p";
            string posterM, posterS;

            // Image sizes:
            // https://api.themoviedb.org/3/configuration?api_key=<key>&language=en-US

            if (movie.backdrop_path != null)
            {
                posterM = baseUrl + "/w780" + movie.backdrop_path;
                posterS = baseUrl + "/w300" + movie.backdrop_path;
            }
            else if (movie.poster_path != null)
            {
                posterM = baseUrl + "/w780" + movie.poster_path;
                posterS = baseUrl + "/w154" + movie.poster_path;
            }
            else
            {
                posterM = null;
                posterS = null;
            }

            return new GetImagesResult
            {
                Medium = posterM,
                Small = posterS
            };
        }
        catch (Exception e)
        {
            throw new Exception($"Failed to retrieve images for {imdbId}", e);
        }
    }
}