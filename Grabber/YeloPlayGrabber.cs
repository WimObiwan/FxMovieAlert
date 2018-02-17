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
    public static class YeloPlayGrabber
    {
        public static readonly string Provider = "yelo";
        public const int ProviderPlayVod = 1;
        public const int ProviderPlay = 2;
        public const int ProviderPlayMore = 4;

        #region JSonModel

        [DebuggerDisplay("display_name = {title}")]
        private class Vod
        {
            public int id { get; set; }
            public string title { get; set; }
            public string imageposter { get; set; }
            public string segments { get; set; }
            public string genre { get; set; }
            public int ppvprice { get; set; }
            public int validfrom { get; set; }
            public int validuntil { get; set; }
            public bool svod { get; set; } // Segment VOD?
            public bool tvod { get; set; } // Pay per view?
        }

        private class YeloPlay
        {
            public List<Vod> vods { get; set; }
        }
        #endregion

        public static List<VodMovie> Get()
        {
            // // Categories from
            // "https://api.yeloplay.be/api/v1/ui/navigation?platform=Web"
            // // ID from 
            // "https://api.yeloplay.be/api/v1/client/mapurl?url=/films/blockbuster"

            string baseUrl = "https://www.yeloplay.be/api/pubba/v1/vods/index/outformat/json/lng/nl/platform/web/";

            Tuple<string, string>[] categories = new Tuple<string, string>[]
            {
                Tuple.Create(
                    "Recent toegevoegd",
                    "id/24906190751/"
                ),
                Tuple.Create(
                    "Populair",
                    "id/20823076751/"
                ),
                Tuple.Create(
                    "Actie",
                    "id/20665051751/"
                ),
                Tuple.Create(
                    "Animatie",
                    "id/21166445751/"
                ),
                Tuple.Create(
                    "Avontuur",
                    "id/21166786751/"
                ),
                Tuple.Create(
                    "Docu",
                    "id/21166787751/"
                ),
                Tuple.Create(
                    "Drama",
                    "id/21166783751/"
                ),
                Tuple.Create(
                    "Familie",
                    "id/21166450751/"
                ),
                Tuple.Create(
                    "Horror",
                    "id/21166451751/"
                ),
                Tuple.Create(
                    "Komedie",
                    "id/20823073751/"
                ),
                Tuple.Create(
                    "Romantiek",
                    "id/21166452751/"
                ),
                Tuple.Create(
                    "Sci-fi & Fantasy",
                    "id/21166784751/"
                ),
                Tuple.Create(
                    "Thriller",
                    "id/20665052751/"
                ),
                Tuple.Create(
                    "Misdaad",
                    "id/96232445751/"
                ),
                Tuple.Create(
                    "Vlaamse Films",
                    "id/21166785751/"
                ),
                Tuple.Create(
                    "Stand up comedy",
                    "id/25135867751/"
                ),
                Tuple.Create(
                    "De keuze van Erik Van Looy",
                    "id/27625874751/"
                ),
                Tuple.Create(
                    "Blockbuster",
                    "id/21166444751/"
                ),
            };

            List<VodMovie> vodMovies = new List<VodMovie>();
            foreach (var category in categories)
            {
                string categoryName = category.Item1;
                string url = baseUrl + category.Item2;
                var request = WebRequest.CreateHttp(url);
                using (var response = request.GetResponse())
                {
                    using (var textStream = new StreamReader(response.GetResponseStream()))
                    {
                        string json = textStream.ReadToEnd();

                        // using (StreamWriter outputFile = new StreamWriter(string.Format(@"humo-{0}.json", dateYMD)))
                        // {
                        //     outputFile.WriteLine(json);
                        // }

                        var yeloPlay = JsonConvert.DeserializeObject<YeloPlay>(json);
                        Merge(vodMovies, categoryName, yeloPlay);
                    }
                }
            }

            return vodMovies;
        }

        private static void Merge(IList<VodMovie> vodMovies, string category, YeloPlay yeloPlay)
        {
            foreach (var vod in yeloPlay.vods)
            {
                var segments = vod.segments.Split(',').Select(s => s.Trim());
                
                int providerMask = 0;
                if (segments.Any(s => s.Equals("base", StringComparison.InvariantCultureIgnoreCase) && vod.ppvprice > 0))
                {
                    providerMask |= ProviderPlayVod;
                }

                if (segments.Any(s => s.Equals("play", StringComparison.InvariantCultureIgnoreCase)))
                {
                    providerMask |= ProviderPlay;
                }

                if (segments.Any(s => s.Equals("play+", StringComparison.InvariantCultureIgnoreCase)))
                {
                    providerMask |= ProviderPlayMore;
                }

                if (providerMask > 0)
                {
                    vodMovies.Add(GetVodMovie(vod, Provider, providerMask, category));
                    Console.WriteLine($"{providerMask} {vod.title}");
                }
            }
        }

        private static VodMovie GetVodMovie(Vod vod, string provider, int providerMask, string category)
        {
            VodMovie vodMovie = new VodMovie();
            vodMovie.Provider = provider;
            vodMovie.ProviderMask = providerMask;
            vodMovie.ProviderCategory = category;
            vodMovie.ProviderId = vod.id;
            vodMovie.Image = vod.imageposter;
            vodMovie.Image_Local = null;
            vodMovie.Title = vod.title;
            vodMovie.Price = vod.ppvprice / 100m;
            vodMovie.ValidFrom =
                (new DateTime (1970, 1, 1)).AddSeconds(vod.validfrom).ToLocalTime();
            vodMovie.ValidUntil =
                (new DateTime (1970, 1, 1)).AddSeconds(vod.validuntil).ToLocalTime();
            return vodMovie;
        }
    }
}
