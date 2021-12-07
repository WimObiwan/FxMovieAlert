// using System;
// using System.Collections.Generic;
// using System.Diagnostics;
// using System.IO;
// using System.Linq;
// using System.Net;
// using System.Text;
// using System.Text.Json;
// using FxMovies.MoviesDB;
// using Sentry;

// namespace FxMovies.Grabber
// {
//     public static class YeloGrabber
//     {
//         #region JSonModel

//         [DebuggerDisplay("title = {program.title}")]
//         private class Broadcast
//         {
//             public int eventid { get; set; }
//             public string mapurl { get; set; }
//             public string title { get; set; }
//             public int starttime { get; set; }
//             public int endtime { get; set; }
//             public string image { get; set; }
//         }

//         [DebuggerDisplay("display_name = {name}")]
//         private class Channel
//         {
//             public string channelid { get; set; }
//             public string name { get; set; }
//             public List<Broadcast> broadcast { get; set; }
//         }

//         private class Yelo
//         {
//             public List<Channel> schedule { get; set; }
//         }
//         #endregion

//         public static void GetGuide(DateTime date, IList<MovieEvent> movieEvents)
//         {
//             // https://www.yeloplay.be/api/pubba/v3/channels/all/outformat/json/platform/web/
//             // channels must be ordered
//             // https://www.yeloplay.be/api/pubba/v1/events/schedule-day/outformat/json/lng/nl/channel/7/channel/22/channel/23/channel/24/channel/198/channel/207/channel/236/channel/258/channel/554/channel/555/channel/567/channel/801/day/2017-09-24/platform/web/

//             var dateYMD = date.ToString("yyyy-MM-dd");
//             var channels = new int[] 
//             {
//                 198, // één
//                 236, // canvas
//                 207, // vtm
//                 567, // q2 --> vtm2
//                 7,   // vitaya --> vtm3
//                 258, // caz = acht --> vtm4
//                 554, // vier
//                 555, // vijf
//                 801, // zes
//                 22,  // npo1
//                 23,  // npo2
//                 24,  // npo3
//                 534, // ketnet
//                 518, // nickelodeon
//                 625, // fox
//                 809, // viceland
//                 283, // vtm kzoom = vtm kids
//                 19,  // vtm jim = vtm kids jr (= kadet?)
//                 291, // disney VL = disney channel?
//             };
//             var sb = new StringBuilder("https://www.yeloplay.be/api/pubba/v1/events/schedule-day/outformat/json/lng/nl");
//             foreach (var channel in channels.OrderBy(c => c))
//             {
//                 sb.AppendFormat("/channel/{0}", channel);
//             }
//             sb.AppendFormat("/day/{0}/platform/web/", dateYMD);
//             string url = sb.ToString();

//             var request = WebRequest.CreateHttp(url);
//             using (var response = request.GetResponse())
//             {
//                 using (var textStream = new StreamReader(response.GetResponseStream()))
//                 {
//                     string json = textStream.ReadToEnd();

//                     // using (StreamWriter outputFile = new StreamWriter(string.Format(@"humo-{0}.json", dateYMD)))
//                     // {
//                     //     outputFile.WriteLine(json);
//                     // }

//                     try 
//                     {
//                         var yelo = JsonSerializer.Deserialize<Yelo>(json);
//                         //var yelo = JsonConvert.DeserializeObject<Yelo>(json);

//                         Merge(movieEvents, yelo);
//                     }
//                     catch (Exception x)
//                     {
//                         Console.WriteLine($"Fetching URL failed: {url}");
//                         Console.WriteLine(x);

//                         SentrySdk.CaptureException(x);
//                     }
//                 }
//             }
//         }

//         static Dictionary<string, string> humoToYeloChannelMapping = new Dictionary<string, string>()
//         {
//             { "eenhd", "198" },
//             { "canvas-hd", "236" },
//             { "vtm-hd", "207" },
//             { "2be-hd", "567" },
//             { "acht", "258" },
//             { "vitaya", "7" },
//             { "vier-hd", "554" },
//             { "vijf-hd", "555" },
//             { "zes", "801" },
//             { "npo1", "22" },
//             { "npo2", "23" },
//             { "npo3", "24" },
//             { "vice", "809" },
//             { "ketnet-op12-hd", "534" },
//             { "nickelodeon-nl", "518" },
//             { "vtmkzoom", "283" },
//             { "fox-hd", "625" },
//             { "kadet", "19" },
//             { "disney-channel-vl", "291" },
//         };
//         private static void Merge(IList<MovieEvent> movieEvents, Yelo yelo)
//         {
//             foreach (var movieEvent in movieEvents)
//             {
//                 string humoChannelId = movieEvent.Channel.Code;
//                 if (!humoToYeloChannelMapping.TryGetValue(humoChannelId, out string yeloChannelId))
//                 {
//                     Console.WriteLine($"WARNING: No Humo to Yelo mapping for channel '{humoChannelId}'");
//                     continue;
//                 }

//                 var yeloChannel = yelo.schedule.Where(c => c.channelid == yeloChannelId).SingleOrDefault();
//                 if (yeloChannel == null)
//                 {
//                     Console.WriteLine($"WARNING: Yelo channel {yeloChannelId}/{humoChannelId} not found in Yelo Channel list");
//                     continue;
//                 }

//                 var time_t = (movieEvent.StartTime.ToUniversalTime() - new DateTime (1970, 1, 1)).TotalSeconds;
//                 var yeloBroadcast = yeloChannel.broadcast.Where(b => b.starttime == time_t).FirstOrDefault();
//                 if (yeloBroadcast == null)
//                 {
//                     Console.WriteLine($"WARNING: {yeloChannelId}/{humoChannelId} {movieEvent.StartTime} {movieEvent.Title} not found in broadcast list");

//                     Console.WriteLine("Other broadcasts:");
//                     foreach (var item in yeloChannel.broadcast.Where (b => b.starttime >= time_t - 3600 * 3 && b.starttime <= time_t + 3600 * 3))
//                     {
//                         DateTime dt = new DateTime(1970, 1, 1).AddSeconds(item.starttime).ToLocalTime();
//                         Console.WriteLine($"   - {dt} {item.title}");
//                     }

//                     continue;
//                 }

//                 Console.WriteLine($"{yeloChannelId}/{humoChannelId} {movieEvent.StartTime} {movieEvent.Title} FOUND {yeloBroadcast.mapurl}");

//                 movieEvent.YeloUrl = "https://www.yeloplay.be" + yeloBroadcast.mapurl;

//                 if (string.IsNullOrEmpty(movieEvent.PosterM))
//                 {
//                     movieEvent.PosterM = yeloBroadcast.image;
//                 }
//                 if (string.IsNullOrEmpty(movieEvent.PosterS))
//                 {
//                     movieEvent.PosterS = yeloBroadcast.image;
//                 }
//             }
//         }
//     }
// }

