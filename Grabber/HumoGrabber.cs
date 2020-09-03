using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using FxMovies.FxMoviesDB;
using Newtonsoft.Json;

namespace FxMovies.Grabber
{
    public static class HumoGrabber
    {
        #region JSonModel

        [DebuggerDisplay("title = {title}")]
        private class HumoBroadcast
        {
            public Guid uuid { get; set; }
            public long from { get; set; }
            public long to { get; set; }
            public int duration { get; set; }
            public string playableType { get; set; }
            public string title { get; set; }
            public string genre { get; set; }
            public string[] subGenres { get; set; }
            public string synopsis { get; set; }
            public string imageUrl { get; set; }
            public int? rating { get; set; }


            public bool IsMovie() => playableType.Equals("movies", StringComparison.InvariantCultureIgnoreCase);
            //public bool IsFirstOfSerieSeason() => program.genres != null && program.genres.Any(g => g.StartsWith("serie-")) && program.episodenumber == 1;

            public bool IsShort() => duration < 3600;

        }

        [DebuggerDisplay("name = {name}")]
        private class HumoChannel
        {
            public string seoKey { get; set; }
            public string name { get; set; }
            public string channelLogoUrl { get; set; }
            public List<HumoBroadcast> broadcasts { get; set; }
        }

        private class Humo
        {
            public List<HumoChannel> channels { get; set; }
        }
        #endregion

        private static async Task<Humo> GetHumoData(string url)
        {
            Console.WriteLine($"Retrieving from Humo: {url}");
            var request = WebRequest.CreateHttp(url);
            using (var response = await request.GetResponseAsync())
            {
                Encoding encoding;
                if (response is HttpWebResponse httpWebResponse)
                {
                    encoding = Encoding.GetEncoding(httpWebResponse.CharacterSet);
                }
                else
                {
                    encoding = Encoding.UTF8;
                }

                using (var textStream = new StreamReader(response.GetResponseStream(), encoding))
                {
                    string json = await textStream.ReadToEndAsync();

                    // using (StreamWriter outputFile = new StreamWriter(string.Format(@"humo-{0}.json", dateYMD)))
                    // {
                    //     outputFile.WriteLine(json);
                    // }

                    var settings = new JsonSerializerSettings();
                    settings.Error += (sender, args) =>
                    {
                        args.ErrorContext.Handled = true;
                    };

                    var humo = JsonConvert.DeserializeObject<Humo>(json, settings);

                    return humo;
                }
            }
        }

        private static async Task<Humo> GetHumoDataWithRetry(string url)
        {
            try 
            {
                return await GetHumoData(url);
            }
            catch (Exception e)
            {
                Console.WriteLine($"First try failed\n{e.Message}");
            }

            await Task.Delay(5000);

            try 
            {
                return await GetHumoData(url);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Second try failed\n{e.Message}");

                throw;
            }
        }

        public static async Task<IList<MovieEvent>> GetGuide(DateTime date)
        {
            string dateYMD = date.ToString("yyyy-MM-dd");
            string url = $"https://www.humo.be/tv-gids/api/v2/broadcasts/{dateYMD}";

            Humo humo = await GetHumoDataWithRetry(url);

            FilterBroadcasters(humo);

            FilterMovies(humo);

            List<MovieEvent> movieEvents = new List<MovieEvent>();
            movieEvents.AddRange(MovieAdapter(humo));
            return movieEvents;
        }

        static string[] channels =
        {
            "een",
            "canvas",
            "vtm",
            "vier",
            "vtm2",
            "vijf",
            "zes",
            "vtm3",
            "vtm4",
            "npo-1",
            "npo-2",
            "npo-3",
            "mtv-vlaanderen",
            "viceland",
            "ketnet",
            "vtm-kids",
            "studio-100-tv",
            "disney-channel",
            "nickelodeon-spike",
            "cartoon24",
        };

        private static void FilterBroadcasters(Humo humo)
        {
            if (humo == null || humo.channels == null)
            {
                return;
            }
            foreach (string channel in channels)
            {
                if (!humo.channels.Any(b => b != null && b.seoKey == channel))
                {
                    Console.WriteLine($"WARNING: No broadcasts found for channel {channel}");
                }
            }

            humo.channels.RemoveAll(b => b == null || !channels.Contains(b.seoKey));
        }

        private static void FilterMovies(Humo humo)
        {
            if (humo == null || humo.channels == null)
            {
                return;
            }

            foreach (var channel in humo.channels)
            {
                //channel.broadcasts.RemoveAll(b => !b.IsMovie() && ! b.IsFirstOfSerieSeason());
                channel.broadcasts.RemoveAll(b => !b.IsMovie());
            }

            humo.channels.RemoveAll(c => (c.broadcasts == null) || (c.broadcasts.Count == 0));
        }

        private static IList<MovieEvent> MovieAdapter(Humo humo)
        {
            if (humo == null || humo.channels == null)
            {
                return new List<MovieEvent>();
            }

            var movies = new List<MovieEvent>();
            foreach (var humoChannel in humo.channels)
            {
                var channel = new Channel()
                {
                    Code = humoChannel.seoKey,
                    Name = humoChannel.name,
                    LogoS = humoChannel.channelLogoUrl,
                };

                foreach (var broadcast in humoChannel.broadcasts)
                {
                    string description = broadcast.synopsis;
                    int? year = null;
                    // int year = broadcast.program.year;

                    // description = description.Replace($" ({year})", "");

                    // if (broadcast.program.episodenumber != 0 && broadcast.program.episodeseason != 0)
                    // {
                    //     description += $" (SERIE: begin van seizoen {broadcast.program.episodeseason})";
                    // }

                    string genre = broadcast.genre?.Trim() ?? "";
                    if (genre != "")
                        genre += " - ";
                    genre += string.Join(' ', broadcast.subGenres);

                    int type;
                    if (broadcast.IsMovie())
                    {
                        if (broadcast.IsShort())
                            type = 2; // short
                        else
                            type = 1; // movie
                    }
                    else
                    {
                        type = 3; // serie
                    }

                    string opinion = null;
                    // string opinion = broadcast.program.opinion;
                    // if (!string.IsNullOrEmpty(broadcast.program.appreciation) 
                    //     && int.TryParse(broadcast.program.appreciation, out int appreciation)
                    //     && appreciation > 0 && appreciation <= 50)
                    // {
                    //     string stars = new string('★', appreciation / 10);
                    //     if (appreciation % 10 > 0)
                    //         stars += '½';
                    //     if (string.IsNullOrEmpty(opinion))
                    //         opinion = stars;
                    //     else
                    //         opinion = stars + " " + opinion;
                    // }
                    if (broadcast.rating.HasValue)
                    {
                        int rating = broadcast.rating.Value;
                        if (rating > 0 && rating <= 100)
                        {
                            string stars = new string('★', rating / 20);
                            if (rating % 20 > 0)
                                stars += '½';
                            opinion = stars;
                        }
                    }

                    var movie = new MovieEvent()
                    {
                        Id = broadcast.uuid.GetHashCode(),
                        Channel = channel,
                        Title = broadcast.title,
                        Year = year,
                        StartTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(broadcast.from / 1000).ToLocalTime(),
                        EndTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(broadcast.to / 1000).ToLocalTime(),
                        Duration = broadcast.duration / 60,
                        PosterS = broadcast.imageUrl,
                        PosterM = broadcast.imageUrl,
                        Content = broadcast.synopsis,
                        Opinion = opinion,
                        Genre = genre,
                        Type = type,
                    };

                    movies.Add(movie);
                }
            }

            return movies;
        }
    }
}
