using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FxMovies.FxMoviesDB;
using Microsoft.Extensions.Configuration;

namespace FxMovies.Grabber
{
    public static class TheMovieDbGrabber
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

        public static string GetCertification(string imdbId)
        {
            // https://api.themoviedb.org/3/movie/tt0114436?api_key=<api_key>&language=en-US&append_to_response=releases

            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .AddEnvironmentVariables()
                .Build();

            string theMovieDbKey = configuration.GetSection("Grabber")["TheMovieDbKey"];
            string certificationCountryPreference = configuration.GetSection("Grabber")["CertificationCountryPreference"];

            string[] certificationCountryPreferenceList = certificationCountryPreference.Split(new char[] {' ', ','}, StringSplitOptions.RemoveEmptyEntries);

            // string url = string.Format("https://api.themoviedb.org/3/movie/{1}?api_key={0}&language=en-US&append_to_response=releases",
            //     theMovieDbKey, imdbId);
            string url = string.Format("https://api.themoviedb.org/3/movie/{1}/releases?api_key={0}&language=en-US",
                theMovieDbKey, imdbId);

            var request = WebRequest.CreateHttp(url);
            try
            {
                using (var response = request.GetResponse())
                {
                    using (var textStream = new StreamReader(response.GetResponseStream()))
                    {
                        string json = textStream.ReadToEnd();

                        var releases = JsonSerializer.Deserialize<Releases>(json);
                        
                        var certifications = releases?.countries;
                        if (certifications == null)
                        {
                            Console.WriteLine("Certification {0} ==> NONE", imdbId);
                            return null;
                        }
                        foreach (var countryId in certificationCountryPreferenceList)
                        foreach (var certification in certifications)
                        {
                            if (certification.iso_3166_1 == countryId && certification.certification != "")
                            {
                                string text = string.Format("{0}:{1}", certification.iso_3166_1, certification.certification);
                                Console.WriteLine("Certification {0} ==> {1}", imdbId, text);
                                return text;
                            }
                        }

                        Console.WriteLine("Certification {0} ==> NOT FOUND IN {1} items", imdbId, certifications.Count);
                    }
                }
            }
            catch (WebException e)
            {
                Console.WriteLine("Certification {0} ==> EXCEPTION {1}", imdbId, e.Message);
            }

            return null;
        }

        public static (string, string) GetImage(string imdbId)
        {
            // https://api.themoviedb.org/3/movie/tt0114436?api_key=<api_key>&language=en-US

            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .AddEnvironmentVariables()
                .Build();

            string theMovieDbKey = configuration.GetSection("Grabber")["TheMovieDbKey"];
            string certificationCountryPreference = configuration.GetSection("Grabber")["CertificationCountryPreference"];

            string url = string.Format("https://api.themoviedb.org/3/movie/{1}?api_key={0}&language=en-US",
                theMovieDbKey, imdbId);

            var request = WebRequest.CreateHttp(url);
            try
            {
                using (var response = request.GetResponse())
                {
                    using (var textStream = new StreamReader(response.GetResponseStream()))
                    {
                        string json = textStream.ReadToEnd();

                        var movie = JsonSerializer.Deserialize<Movie>(json);
                        
                        Console.WriteLine("Image {0} ==> {1}", imdbId, movie.original_title);
                        
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

                        return (posterM, posterS);
                    }
                }
            }
            catch (WebException e)
            {
                Console.WriteLine("Image {0} ==> EXCEPTION {1}", imdbId, e.Message);
            }

            return (null, null);
        }
    }
}
