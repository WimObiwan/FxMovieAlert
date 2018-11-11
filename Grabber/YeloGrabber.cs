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
    public static class YeloGrabber
    {
        #region JSonModel

        [DebuggerDisplay("title = {program.title}")]
        private class Broadcast
        {
            public int eventid { get; set; }
            public string mapurl { get; set; }
            public string title { get; set; }
            public int starttime { get; set; }
            public int endtime { get; set; }
            public string image { get; set; }
        }

        [DebuggerDisplay("display_name = {name}")]
        private class Channel
        {
            public string channelid { get; set; }
            public string name { get; set; }
            public List<Broadcast> broadcast { get; set; }
        }

        private class Yelo
        {
            public List<Channel> schedule { get; set; }
        }
        #endregion

        public static void GetGuide(DateTime date, IList<MovieEvent> movieEvents)
        {
            // https://www.yeloplay.be/api/pubba/v3/channels/all/outformat/json/platform/web/
            // één 198
            // canvas 236
            // vtm 207
            // q2 567
            // vitaya 7
            // caz 258
            // vier 554
            // vijf 555
            // zes 801 
            // npo1 22
            // npo2 23
            // npo3 24
            // channels must be ordered
            // https://www.yeloplay.be/api/pubba/v1/events/schedule-day/outformat/json/lng/nl/channel/7/channel/22/channel/23/channel/24/channel/198/channel/207/channel/236/channel/258/channel/554/channel/555/channel/567/channel/801/day/2017-09-24/platform/web/

            var dateYMD = date.ToString("yyyy-MM-dd");
            var channels = new int[] {198, 236, 207, 567, 7, 258, 554, 555, 801, 22, 23, 24};
            var sb = new StringBuilder("https://www.yeloplay.be/api/pubba/v1/events/schedule-day/outformat/json/lng/nl");
            foreach (var channel in channels.OrderBy(c => c))
            {
                sb.AppendFormat("/channel/{0}", channel);
            }
            sb.AppendFormat("/day/{0}/platform/web/", dateYMD);
            string url = sb.ToString();

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

                    var yelo = JsonConvert.DeserializeObject<Yelo>(json);

                    Merge(movieEvents, yelo);
                }
            }
        }

        private static void Merge(IList<MovieEvent> movieEvents, Yelo yelo)
        {
            foreach (var movieEvent in movieEvents)
            {
                string channel = movieEvent.Channel.Name;
                var yeloChannel = yelo.schedule.Where(c => 
                    c.name.Equals(channel, StringComparison.InvariantCultureIgnoreCase) 
                    || c.name.Equals(channel + " HD", StringComparison.InvariantCultureIgnoreCase)
                    ).FirstOrDefault();
                if (yeloChannel == null)
                {
                    Console.WriteLine("Channel {0} not found in Yelo Channel list", channel);
                    continue;
                }
                var time_t = (movieEvent.StartTime.ToUniversalTime() - new DateTime (1970, 1, 1)).TotalSeconds;
                var yeloBroadcast = yeloChannel.broadcast.Where(b => b.starttime == time_t).FirstOrDefault();
                if (yeloBroadcast == null)
                {
                    Console.WriteLine("{0} {1} {2} not found in broadcast list", channel, movieEvent.StartTime, movieEvent.Title);
                    continue;
                }

                Console.WriteLine("{0} {1} {2} FOUND {3}", channel, movieEvent.StartTime, movieEvent.Title, yeloBroadcast.mapurl);

                movieEvent.YeloUrl = "https://www.yeloplay.be" + yeloBroadcast.mapurl;

                if (string.IsNullOrEmpty(movieEvent.PosterM))
                {
                    movieEvent.PosterM = yeloBroadcast.image;
                }
                if (string.IsNullOrEmpty(movieEvent.PosterS))
                {
                    movieEvent.PosterS = yeloBroadcast.image;
                }
            }
        }
    }
}
