using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FxMovies.Core
{
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
        public string CertificationCountryPreference { get; set; }
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
        private readonly string apiKey;
        private readonly string[] certificationCountryPreferenceList;

        public TheMovieDbService(ILogger<TheMovieDbServiceOptions> logger, IOptionsSnapshot<TheMovieDbServiceOptions> options)
        {
            this.logger = logger;
            var o = options.Value;
            this.apiKey = o.ApiKey;
            this.certificationCountryPreferenceList = o.CertificationCountryPreference?.Split(new char[] {' ', ','}, StringSplitOptions.RemoveEmptyEntries);
        }

        public async Task<string> GetCertification(string imdbId)
        {
            if (string.IsNullOrEmpty(apiKey) || certificationCountryPreferenceList == null)
                return "";

            // https://api.themoviedb.org/3/movie/tt0114436?api_key=<api_key>&language=en-US&append_to_response=releases

            // string url = string.Format("https://api.themoviedb.org/3/movie/{1}?api_key={0}&language=en-US&append_to_response=releases",
            //     theMovieDbKey, imdbId);
            string url = string.Format("https://api.themoviedb.org/3/movie/{1}/releases?api_key={0}&language=en-US",
                apiKey, imdbId);

            var request = WebRequest.CreateHttp(url);
            try
            {
                using (var response = await request.GetResponseAsync())
                using (var stream = response.GetResponseStream())
                {
                    var releases = await JsonSerializer.DeserializeAsync<Releases>(stream);
                    
                    var certifications = releases?.countries;
                    if (certifications == null)
                    {
                        logger.LogInformation($"Certification {imdbId} ==> NONE");
                        return null;
                    }
                    foreach (var countryId in certificationCountryPreferenceList)
                    {
                        var certification = certifications.FirstOrDefault(c => c.iso_3166_1 == countryId && !string.IsNullOrEmpty(c.certification));
                        if (certification != null)
                        {
                            string text = $"{certification.iso_3166_1}:{certification.certification}";
                            logger.LogInformation($"Certification {imdbId} ==> {text}");
                            return text;
                        }
                    }

                    logger.LogInformation($"Certification {imdbId} ==> NOT FOUND IN {certifications.Count} items");
                }
            }
            catch (WebException x)
            {
                logger.LogError(x, $"Certification {imdbId} ==> EXCEPTION");
            }

            return null;
        }

        public async Task<GetImagesResult> GetImages(string imdbId)
        {
            // https://api.themoviedb.org/3/movie/tt0114436?api_key=<api_key>&language=en-US

            string url = string.Format("https://api.themoviedb.org/3/movie/{1}?api_key={0}&language=en-US",
                apiKey, imdbId);

            var request = WebRequest.CreateHttp(url);
            try
            {
                using (var response = await request.GetResponseAsync())
                using (var stream = response.GetResponseStream())
                {
                    var movie = await JsonSerializer.DeserializeAsync<Movie>(stream);
                    
                    logger.LogInformation($"Image {imdbId} ==> {movie.original_title}");
                    
                    string baseUrl = "http://image.tmdb.org/t/p";
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
            }
            catch (Exception e)
            {
                throw new Exception($"Failed to retrieve images for {imdbId}", e);
            }
        }
    }
}
