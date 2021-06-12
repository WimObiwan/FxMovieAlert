using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml;
using FxMovies.FxMoviesDB;
using FxMovies.ImdbDB;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using CommandLine;
using CommandLine.Text;
using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using FxMovies.Core;

// Compile: 

namespace FxMovies.Grabber
{
    class Program
    {
        public class TemporaryFxMoviesDbContextFactory : IDesignTimeDbContextFactory<FxMoviesDbContext>
        {
            public FxMoviesDbContext CreateDbContext(string[] args)
            {
                var builder = new DbContextOptionsBuilder<FxMoviesDbContext>();

                var configuration = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                    .AddEnvironmentVariables()
                    .Build();

                // Get the connection string
                string connectionString = configuration.GetConnectionString("FxMoviesDb");

                builder.UseSqlite(connectionString); 
                return new FxMoviesDbContext(builder.Options); 
            }
        }

        public class TemporaryImdbDbContextFactory : IDesignTimeDbContextFactory<ImdbDbContext>
        {
            public ImdbDbContext CreateDbContext(string[] args)
            {
                var builder = new DbContextOptionsBuilder<ImdbDbContext>();

                var configuration = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                    .AddEnvironmentVariables()
                    .Build();

                // Get the connection string
                string connectionString = configuration.GetConnectionString("ImdbDb");

                builder.UseSqlite(connectionString); 
                return new ImdbDbContext(builder.Options); 
            }
        }

        [Verb("Help", HelpText = "Shows help about Grabber.")]
        class HelpOptions
        {
        }

        [Verb("GenerateImdbDatabase", HelpText = "Generate IMDb database.")]
        class GenerateImdbDatabaseOptions
        {
        }

        [Verb("UpdateImdbUserRatings", HelpText = "Update IMDb user ratings.")]
        class UpdateImdbUserRatingsOptions
        {
            [Option("userid", Required = true, HelpText = "User ID")]
            public string UserId { get; set; }
            [Option("imdbuserid", Required = true, HelpText = "IMDb user ID")]
            public string ImdbUserId { get; set; }
        }

        [Verb("AutoUpdateImdbUserRatings", HelpText = "Auto update IMDb user ratings.")]
        class AutoUpdateImdbUserRatingsOptions
        {
        }

        [Verb("UpdateEPG", HelpText = "Update EPG.")]
        class UpdateEpgOptions
        {
        }

        // [Verb("UpdateVod", HelpText = "Update VOD.")]
        // class UpdateVodOptions
        // {
        // }

        // [Verb("TwitterBot", HelpText = "Twitter Bot.")]
        // class TwitterBotOptions
        // {
        // }

        // [Verb("Manual", HelpText = "Manual.")]
        // class ManualOptions
        // {
        //     [Option("movieeventid", Required = true, HelpText = "Movie event ID")]
        //     public int MovieEventId { get; set; }
        //     [Option("imdbid", Required = true, HelpText = "IMDb ID")]
        //     public string ImdbId { get; set; }
        // }

        [Verb("TestSentry", HelpText = "Test Sentry.")]
        class TestSentryOptions
        {
        }

        static int Main(string[] args)
        {
            using (var host = CreateHostBuilder(args).Build())
            {
                Console.WriteLine("Version " +
                    ThisAssembly.Git.SemVer.Major + "." +
                    ThisAssembly.Git.SemVer.Minor + "." +
                    ThisAssembly.Git.Commits + "-" +
                    ThisAssembly.Git.Branch + "+" +
                    ThisAssembly.Git.Commit
                    //doesn't work (ThisAssembly.Git.IsDirty ? "*" : "")
                    );


                using (var db = host.Services.GetRequiredService<IDbContextFactory<FxMoviesDbContext>>().CreateDbContext())
                {
                    // Ensure that the SQLite database and sechema is created!
                    db.Database.EnsureCreated();
                }

                using (var db = host.Services.GetRequiredService<IDbContextFactory<ImdbDbContext>>().CreateDbContext())
                {
                    // Ensure that the SQLite database and sechema is created!
                    db.Database.EnsureCreated();
                }

                using (Sentry.SentrySdk.Init("https://3181503fa0264cdb827506614c8973f2@sentry.io/1335361"))
                {
                    return Parser.Default
                        .ParseArguments<
                            HelpOptions,
                            GenerateImdbDatabaseOptions,
                            // UpdateImdbUserRatingsOptions,
                            // AutoUpdateImdbUserRatingsOptions,
                            UpdateEpgOptions,
                            // UpdateVodOptions,
                            // TwitterBotOptions,
                            // ManualOptions,
                            TestSentryOptions
                            >(args)
                        .MapResult(
                            (HelpOptions o) => Run(o),
                            (GenerateImdbDatabaseOptions o) => Run(o),
                            // (UpdateImdbUserRatingsOptions o) => Run(o),
                            // (AutoUpdateImdbUserRatingsOptions o) => Run(o),
                            (UpdateEpgOptions o) => host.Services.GetRequiredService<IUpdateEpgCommand>().Run(),
                            // (UpdateVodOptions o) => Run(o),
                            // (TwitterBotOptions o) => Run(o),
                            // (ManualOptions o) => Run(o),
                            (TestSentryOptions o) => Run(o),
                            errs => 1);
                }
            }
        }

        static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.AddInMemoryCollection(
                        new List<KeyValuePair<string, string>>()
                        {
                            new KeyValuePair<string, string>("Version", 
                                ThisAssembly.Git.SemVer.Major + "." +
                                ThisAssembly.Git.SemVer.Minor + "." +
                                ThisAssembly.Git.Commits + "-" +
                                ThisAssembly.Git.Branch + "+" +
                                ThisAssembly.Git.Commit
                                //doesn't work (ThisAssembly.Git.IsDirty ? "*" : "")
                                ),
                            new KeyValuePair<string, string>("DotNetCoreVersion", 
                                System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription)

                        }
                    );
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
                    config.AddJsonFile($"appsettings.{hostingContext.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: false);
                    config.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);
                    config.AddEnvironmentVariables();
                })
                .UseStartup<Startup>();
        }

        private static int Run(HelpOptions options)
        {
            return 0;
        }

        private static int Run(GenerateImdbDatabaseOptions options)
        {
            ImportImdbData_Movies();
            ImportImdbData_AlsoKnownAs();
            ImportImdbData_Ratings();
            ImportImdbData_CleanupMoviesWithoutRatings();
            ImportImdbData_Vacuum();
            return 0;
        }

        // private static int Run(UpdateImdbUserRatingsOptions options)
        // {
        //     UpdateImdbUserRatings(options.UserId, options.ImdbUserId);
        //     return 0;
        // }

        // private static int Run(AutoUpdateImdbUserRatingsOptions options)
        // {
        //     AutoUpdateImdbUserRatings();
        //     return 0;
        // }

        // private static int Run(UpdateVodOptions options)
        // {
        //     UpdateDatabaseVod_YeloPlay();
        //     UpdateVodDataWithImdb();
        //     return 0;
        // }

        // private static int Run(TwitterBotOptions options)
        // {
        //     TwitterBot();
        //     return 0;
        // }

        // private static int Run(ManualOptions options)
        // {
        //     UpdateEpgDataWithImdbManual(options.MovieEventId, options.ImdbId);
        //     return 0;
        // }

        private static int Run(TestSentryOptions options)
        {
            throw new Exception("Test Sentry");
        }

        // sqlite3 /tmp/imdb.db "VACUUM;" -- 121MB => 103 MB

        // static void UpdateImdbUserRatings(string userId, string imdbUserId)
        // {
        //     UpdateImdbUserRatings(userId, imdbUserId, false);
        //     UpdateImdbUserRatings(userId, imdbUserId, true);

        //     var configuration = new ConfigurationBuilder()
        //         .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
        //         .AddEnvironmentVariables()
        //         .Build();

        //     // Get the connection string
        //     string connectionString = configuration.GetConnectionString("FxMoviesDb");

        //     using (var db = FxMoviesDbContextFactory.Create(connectionString))
        //     {
        //         User user = db.Users.Find(imdbUserId);
        //         if (user == null)
        //         {
        //             user = new User();
        //             user.ImdbUserId = imdbUserId;
        //             db.Users.Add(user);
        //         }
        //         user.RefreshRequestTime = null;
        //         user.RefreshCount++;

        //         db.SaveChanges();
        //     }
        // }

        // static void UpdateImdbUserRatings(string userId, string imdbUserId, bool watchlist)
        // {
        //     var configuration = new ConfigurationBuilder()
        //         .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
        //         .AddEnvironmentVariables()
        //         .Build();

        //     // Get the connection string
        //     string connectionString = configuration.GetConnectionString("FxMoviesDb");

        //     Console.WriteLine("Using database: {0}", connectionString);

        //     var regexImdbId = new Regex(@"/(tt\d+)/", RegexOptions.Compiled);
        //     var regexRating = new Regex(@"rated this (\d+)\.", RegexOptions.Compiled);
        //         DateTime now = DateTime.Now;
        //         string result;
        //         bool succeeded;

        //     try
        //     {
        //         string suffix = watchlist ? "watchlist" : "ratings";
        //         string url = $"http://rss.imdb.com/user/{imdbUserId}/{suffix}";
        //         var request = (HttpWebRequest)WebRequest.Create(url);
        //         using (var response = request.GetResponse())
        //         {
        //             var xmlDocument = new XmlDocument();
        //             xmlDocument.Load(response.GetResponseStream());

        //             int count = xmlDocument.DocumentElement["channel"].ChildNodes.Count;
        //             string lastDescription = null;
        //             DateTime? lastDate = null;

        //             foreach (XmlNode item in xmlDocument.DocumentElement["channel"].ChildNodes)
        //             {
        //                 if (item.Name != "item")
        //                     continue;
                        
        //                 Console.WriteLine("UpdateImdbUserRatings: {0} - {1} - {2}", item["pubDate"].InnerText, item["title"].InnerText, item["description"].InnerText);

        //                 string imdbId = regexImdbId.Match(item["link"].InnerText)?.Groups?[1]?.Value;
        //                 if (imdbId == null)
        //                     continue;
                        
        //                 string description = item["description"].InnerText.Trim();

        //                 DateTime date = DateTime.Parse(item["pubDate"].InnerText, CultureInfo.InvariantCulture.DateTimeFormat);

        //                 using (var db = FxMoviesDbContextFactory.Create(connectionString))
        //                 {
        //                     if (watchlist)
        //                     {
        //                         var userWatchListItem = db.UserWatchLists.Find(userId, imdbId);
        //                         if (userWatchListItem == null)
        //                         {
        //                             userWatchListItem = new UserWatchListItem();
        //                             userWatchListItem.UserId = userId;
        //                             userWatchListItem.ImdbMovieId = imdbId;
        //                             db.UserWatchLists.Add(userWatchListItem);
        //                         }
        //                         userWatchListItem.AddedDate = date;

        //                         Console.WriteLine("UserId={0} IMDbId={1} Added={2}", 
        //                             userWatchListItem.UserId, userWatchListItem.ImdbMovieId, userWatchListItem.AddedDate);
        //                     }
        //                     else
        //                     {
        //                         string ratingText = regexRating.Match(description)?.Groups?[1]?.Value;
        //                         if (ratingText == null)
        //                             continue;
        //                         int rating = int.Parse(ratingText);

        //                         var userRating = db.UserRatings.Find(userId, imdbId);
        //                         if (userRating == null)
        //                         {
        //                             userRating = new UserRating();
        //                             userRating.UserId = userId;
        //                             userRating.ImdbMovieId = imdbId;
        //                             db.UserRatings.Add(userRating);
        //                         }
        //                         userRating.RatingDate = date;
        //                         userRating.Rating = rating;

        //                         Console.WriteLine("UserId={0} IMDbId={1} Added={2} Rating={3}", 
        //                             userRating.UserId, userRating.ImdbMovieId, userRating.RatingDate, userRating.Rating);
        //                     }

        //                     db.SaveChanges();
        //                 }

        //                 if (date > lastDate.GetValueOrDefault(DateTime.MinValue))
        //                 {
        //                     lastDate = date;
        //                     lastDescription = description;
        //                 }
        //             }

        //             Console.WriteLine("UpdateImdbUserRatings: Loaded {0} ratings", count);
        //             result = string.Format("{0} ratings geladen.", count);
        //             if (lastDate.HasValue)
        //             {
        //                 result += string.Format("  Laatste rating gebeurde op {0} (\"{1}\")", lastDate.Value.ToString("yyyy-MM-dd"), lastDescription);
        //             }
        //             succeeded = true;
        //         }
        //     }
        //     catch (WebException x)
        //     {
        //         result = "Foutmelding: " + x.Message;
        //         succeeded = false;
        //     }

        //     using (var db = FxMoviesDbContextFactory.Create(connectionString))
        //     {
        //         User user = db.Users.Find(imdbUserId);
        //         if (user == null)
        //         {
        //             user = new User();
        //             user.ImdbUserId = imdbUserId;
        //             db.Users.Add(user);
        //         }
        //         if (watchlist)
        //         {
        //             user.WatchListLastRefreshTime = DateTime.UtcNow;
        //             user.WatchListLastRefreshResult = result;
        //             user.WatchListLastRefreshSuccess = succeeded;
        //         }
        //         else
        //         {
        //             user.LastRefreshRatingsTime = DateTime.UtcNow;
        //             user.LastRefreshRatingsResult = result;
        //             user.LastRefreshSuccess = succeeded;
        //         }

        //         db.SaveChanges();
        //     }            
        // }

        // static void AutoUpdateImdbUserRatings()
        // {
        //     var configuration = new ConfigurationBuilder()
        //         .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
        //         .AddEnvironmentVariables()
        //         .Build();

        //     // Get the connection string
        //     string connectionString = configuration.GetConnectionString("FxMoviesDb");

        //     Console.WriteLine("Using database: {0}", connectionString);

        //     IList<User> users;

        //     var refreshTime = DateTime.UtcNow.AddDays(-1);
        //     using (var db = FxMoviesDbContextFactory.Create(connectionString))
        //     {
        //         users = db.Users.Where (u => 
        //             u.RefreshRequestTime.HasValue || // requested to be refreshed, OR
        //             !u.LastRefreshRatingsTime.HasValue || // never refreshed before, OR
        //             u.LastRefreshRatingsTime.Value < refreshTime).ToList(); // last refresh is 24 hours ago
        //     }

        //     foreach (var user in users)
        //     {
        //         Console.WriteLine("User {0} needs a refresh of the IMDb User ratings", user.ImdbUserId);
        //         if (user.RefreshRequestTime.HasValue)
        //             Console.WriteLine("   * RefreshRequestTime = {0} ({1} seconds ago)", 
        //                 user.RefreshRequestTime.Value, (refreshTime - user.RefreshRequestTime.Value).TotalSeconds);
        //         if (!user.LastRefreshRatingsTime.HasValue)
        //             Console.WriteLine("   * LastRefreshRatingsTime = null");
        //         else 
        //             Console.WriteLine("   * LastRefreshRatingsTime = {0}", 
        //                 user.LastRefreshRatingsTime.Value);
                    
        //         UpdateImdbUserRatings(user.UserId, user.ImdbUserId);
        //     }
        // }

        // static void UpdateDatabaseVod_YeloPlay()
        // {
        //     List<VodMovie> vodMovies = YeloPlayGrabber.Get();

        //     var configuration = new ConfigurationBuilder()
        //         .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
        //         .AddEnvironmentVariables()
        //         .Build();

        //     // Get the connection string
        //     string connectionString = configuration.GetConnectionString("FxMoviesDb");

        //     Console.WriteLine("Using database: {0}", connectionString);

        //     using (var db = FxMoviesDbContextFactory.Create(connectionString))
        //     {
        //         var existingMovies = db.VodMovies.Where(m => YeloPlayGrabber.Provider == m.Provider);
        //         // Console.WriteLine("Existing movies: {0}", existingMovies.Count());
        //         // Console.WriteLine("New movies: {0}", movies.Count());

        //         // Remove exising movies that don't appear in new movies
        //         {
        //             var remove = existingMovies.Where(m1 => 
        //                 !vodMovies.Any(
        //                     m2 => m2.Provider == m1.Provider
        //                     && m2.ProviderCategory == m1.ProviderCategory
        //                     && m2.ProviderId == m1.ProviderId));
        //             Console.WriteLine("Existing movies to be removed: {0}", remove.Count());
        //             db.RemoveRange(remove);
        //         }

        //         // Update movies
        //         foreach (var vodMovie in vodMovies)
        //         {
        //             var existingVodMovie = db.VodMovies.Find(vodMovie.Provider, vodMovie.ProviderCategory, vodMovie.ProviderId);
        //             if (existingVodMovie != null)
        //             {
        //                 if (existingVodMovie.Title != vodMovie.Title)
        //                 {
        //                     existingVodMovie.Title = vodMovie.Title;
        //                     existingVodMovie.ImdbId = null;
        //                     existingVodMovie.ImdbRating = null;
        //                     existingVodMovie.ImdbVotes = null;
        //                     existingVodMovie.Certification = null;
        //                 }
        //                 if (existingVodMovie.Image != vodMovie.Image)
        //                 {
        //                     existingVodMovie.Image = vodMovie.Image;
        //                     existingVodMovie.Image_Local = null;
        //                 }
        //                 existingVodMovie.ProviderMask = vodMovie.ProviderMask;
        //                 existingVodMovie.Price = vodMovie.Price;
        //                 existingVodMovie.ValidFrom = vodMovie.ValidFrom;
        //                 existingVodMovie.ValidUntil = vodMovie.ValidUntil;
        //             }
        //             else
        //             {
        //                 db.VodMovies.Add(vodMovie);
        //             }
        //         }

        //         // {
        //         //     set.RemoveRange(set.Where(x => x.StartTime.Date == date));
        //         //     db.SaveChanges();
        //         // }

        //         db.SaveChanges();
        //     }
        // }

        // static void UpdateDatabaseEpgHistory()
        // {
        //     var configuration = new ConfigurationBuilder()
        //         .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
        //         .AddEnvironmentVariables()
        //         .Build();

        //     // Get the connection string
        //     string connectionString = configuration.GetConnectionString("FxMoviesDb");
        //     string connectionStringHistory = configuration.GetConnectionString("FxMoviesHistoryDb");

        //     Console.WriteLine("Using database: {0}", connectionString);
        //     Console.WriteLine("Using database: {0}", connectionStringHistory);

        //     using (var db = FxMoviesDbContextFactory.Create(connectionString))
        //     using (var dbHistory = FxMoviesDbContextFactory.Create(connectionStringHistory))
        //     {
        //         foreach (var channel in db.Channels)
        //         {
        //             var channelHistory = dbHistory.Channels.Find(channel.Code);
        //             if (channelHistory == null)
        //             {
        //                 channelHistory = new Channel();
        //                 channelHistory.Code = channel.Code;
        //                 dbHistory.Channels.Add(channelHistory);
        //             }
        //             channelHistory.Name = channel.Name;
        //             channelHistory.LogoS = channel.LogoS;
        //             channelHistory.LogoS_Local = channel.LogoS_Local;
        //         }
        //         dbHistory.SaveChanges();

        //         var min = db.MovieEvents.Where(i => i.StartTime >= DateTime.Now).Select(i => i.StartTime).Min();
        //         var movieEventsToRemove = dbHistory.MovieEvents.Where(i => i.StartTime >= min);
        //         dbHistory.MovieEvents.RemoveRange(movieEventsToRemove);
        //         int lastId = dbHistory.MovieEvents.Select(i => (int?)i.Id).Max() ?? 0;
        //         foreach (var movieEvent in db.MovieEvents)
        //         {
        //             var movieEventHistory = dbHistory.MovieEvents.Find(movieEvent.Id);
        //             if (movieEventHistory != null)
        //                 dbHistory.MovieEvents.Remove(movieEventHistory);

        //             var channelHistory = dbHistory.Channels.Find(movieEvent.Channel.Code);

        //             movieEventHistory = new MovieEvent();
        //             movieEventHistory.Id = ++lastId;
        //             movieEventHistory.Title = movieEvent.Title;
        //             movieEventHistory.Year = movieEvent.Year ?? 0;
        //             movieEventHistory.Vod = movieEvent.Vod;                    
        //             movieEventHistory.StartTime = movieEvent.StartTime;
        //             movieEventHistory.EndTime = movieEvent.EndTime;
        //             movieEventHistory.Channel = channelHistory;
        //             movieEventHistory.PosterS = movieEvent.PosterS;
        //             movieEventHistory.PosterM = movieEvent.PosterM;
        //             movieEventHistory.Duration = movieEvent.Duration;
        //             movieEventHistory.Genre = movieEvent.Genre;
        //             movieEventHistory.Content = movieEvent.Content;
        //             movieEventHistory.ImdbId = movieEvent.ImdbId;
        //             movieEventHistory.ImdbRating = movieEvent.ImdbRating;
        //             movieEventHistory.ImdbVotes = movieEvent.ImdbVotes;
        //             movieEventHistory.YeloUrl = null;
        //             movieEventHistory.Certification = movieEvent.Certification;
        //             movieEventHistory.PosterS_Local = movieEvent.PosterS_Local;
        //             movieEventHistory.PosterM_Local = movieEvent.PosterM_Local;
        //             dbHistory.MovieEvents.Add(movieEventHistory);
        //         }
        //         dbHistory.SaveChanges();
        //     }
        // }

        // static void DownloadFile(string url, string target, ref string eTag, ref DateTime? lastMod)
        // {
        //     var req = (HttpWebRequest)WebRequest.Create(url);
        //     if (lastMod.HasValue)
        //         req.IfModifiedSince = lastMod.Value;//note: must be UTC, use lastMod.Value.ToUniversalTime() if you store it somewhere that converts to localtime, like SQLServer does.
        //     if (eTag != null)
        //         req.Headers.Add("If-None-Match", eTag);
        //     try
        //     {
        //         using (var rsp = (HttpWebResponse)req.GetResponse())
        //         {
        //             lastMod = rsp.LastModified;
        //             if (lastMod.Value.Year == 1)//wasn't sent. We're just going to have to download the whole thing next time to be sure.
        //                 lastMod = null;
        //             eTag = rsp.GetResponseHeader("ETag");//will be null if absent.
        //             using (var stm = rsp.GetResponseStream())
        //             using (var fileStream = File.Create(target))
        //             {
        //                 stm.CopyTo(fileStream);
        //             }
        //         }
        //     }
        //     catch (WebException we)
        //     {
        //         var hrsp = we.Response as HttpWebResponse;
        //         if (hrsp != null && hrsp.StatusCode == HttpStatusCode.NotModified)
        //         {
        //             //unfortunately, 304 when dealt with directly (rather than letting
        //             //the IE cache be used automatically), is treated as an error. Which is a bit of
        //             //a nuisance, but manageable. Note that if we weren't doing this manually,
        //             //304s would be disguised to look like 200s to our code.

        //             //update these, because possibly only one of them was the same.
        //             lastMod = hrsp.LastModified;
        //             if (lastMod.Value.Year == 1)//wasn't sent.
        //                 lastMod = null;
        //             eTag = hrsp.GetResponseHeader("ETag");//will be null if absent.
        //         }
        //         else //some other exception happened!
        //             throw; //or other handling of your choosing
        //     }
        // }

        // static void UpdateVodDataWithImdb()
        // {
        //     UpdateGenericDataWithImdb<VodMovie>((dbMovies) => dbMovies.VodMovies);
        // }

        static void ImportImdbData_Movies()
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .AddEnvironmentVariables()
                .Build();

            // Get the connection string
            string connectionString = configuration.GetConnectionString("ImdbDb");
            if (!int.TryParse(configuration.GetSection("Grabber")["DebugMaxImdbRowCount"], out int debugMaxImdbRowCount))
                debugMaxImdbRowCount = 0;

            Console.WriteLine("Removing MovieAlternatives");
            do {
                using (var db = ImdbDbContextFactory.Create(connectionString))
                {
                    var batch = db.MovieAlternatives.AsNoTracking().Take(10000);
                    if (!batch.Any())
                        break;
                    db.MovieAlternatives.RemoveRange(batch);
                    db.SaveChanges();
                }
            } while (true);

            Console.WriteLine("Removing Movies");
            do {
                using (var db = ImdbDbContextFactory.Create(connectionString))
                {
                    var batch = db.Movies.Take(10000);
                    if (!batch.Any())
                        break;
                    db.Movies.RemoveRange(batch);
                    db.SaveChanges();
                }
            } while (true);

            var fileToDecompress = new FileInfo(configuration.GetSection("Grabber")["ImdbMoviesList"]);
            using (var originalFileStream = fileToDecompress.OpenRead())
            using (var decompressionStream = new GZipStream(originalFileStream, CompressionMode.Decompress))
            using (var textReader = new StreamReader(decompressionStream))
            {
                int count = 0, countAlternatives = 0, skipped = 0;
                string text = null;

                // tconst	titleType	primaryTitle	originalTitle	isAdult	startYear	endYear	runtimeMinutes	genres
                // tt0000009	movie	Miss Jerry	Miss Jerry	0	1894	\N	45	Romance
                var regex = new Regex(@"^([^\t]*)\t([^\t]*)\t([^\t]*)\t([^\t]*)\t[^\t]*\t([^\t]*)\t[^\t]*\t[^\t]*\t[^\t]*$",
                    RegexOptions.Compiled);

                var stopwatch = new Stopwatch();
                stopwatch.Start();

                // Skip header
                textReader.ReadLine();

                var FilterTypes = new string[]
                {
                    "movie",
                    "video",
                    "short",
                    "tvMovie",
                    "tvMiniSeries",
                    "tvSeries",
                };

                do {
                    using (var db = ImdbDbContextFactory.Create(connectionString))
                    {
                        int batchCount = 0;
                        while (batchCount < 10000 && (text = textReader.ReadLine()) != null)
                        {
                            batchCount++;
                            count++;

                            if (count % 10000 == 0)
                            {
                                Console.WriteLine("UpdateImdbDataWithMovies: {1} records done ({0}%), {2} alternatives, {3} records skipped ({4}%), {5}", 
                                    originalFileStream.Position * 100 / originalFileStream.Length, 
                                    count,
                                    countAlternatives, 
                                    skipped,
                                    skipped * 100 / count,
                                    stopwatch.ElapsedMilliseconds);
                                stopwatch.Restart();                            

                                // For debugging
                                if (debugMaxImdbRowCount > 0 && count >= debugMaxImdbRowCount)
                                    break;
                            }

                            var match = regex.Match(text);
                            if (!match.Success)
                            {
                                Console.WriteLine("Unable to parse line {0}: {1}", count, text);
                                continue;
                            }

                            // Filter on movie|video|short|tvMovie|tvMiniSeries
                            if (!FilterTypes.Contains(match.Groups[2].Value))
                            {
                                skipped++;
                                continue;
                            }

                            string movieId = match.Groups[1].Value;

                            var movie = new ImdbDB.Movie();
                            movie.ImdbId = movieId;
                            movie.PrimaryTitle = match.Groups[3].Value;
                            string originalTitle = match.Groups[4].Value;

                            if (int.TryParse(match.Groups[5].Value, out int startYear))
                                movie.Year = startYear;

                            string primaryTitleNormalized = ImdbDB.Util.NormalizeTitle(movie.PrimaryTitle);

                            countAlternatives++;
                            movie.MovieAlternatives = new List<MovieAlternative>
                            {
                                new MovieAlternative()
                                {
                                    Movie = movie,
                                    AlternativeTitle = null,
                                    Normalized = primaryTitleNormalized
                                }
                            };
                            
                            string originalTitleNormalized = ImdbDB.Util.NormalizeTitle(originalTitle);
                            if (primaryTitleNormalized != originalTitleNormalized)
                            {
                                countAlternatives++;
                                movie.MovieAlternatives.Add(
                                    new MovieAlternative()
                                    {
                                        Movie = movie,
                                        AlternativeTitle = originalTitle,
                                        Normalized = originalTitleNormalized
                                    });
                            }

                            db.Movies.Add(movie);
                        }

                        db.SaveChanges();
                    }
                } while (text != null);
                
                Console.WriteLine("IMDb movies scanned: {0}", count);
            }
        }        

        static void ImportImdbData_AlsoKnownAs()
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .AddEnvironmentVariables()
                .Build();

            // Get the connection string
            string connectionString = configuration.GetConnectionString("ImdbDb");
            if (!int.TryParse(configuration.GetSection("Grabber")["DebugMaxImdbRowCount"], out int debugMaxImdbRowCount))
                debugMaxImdbRowCount = 0;

            var fileToDecompress = new FileInfo(configuration.GetSection("Grabber")["ImdbAlsoKnownAsList"]);
            using (var originalFileStream = fileToDecompress.OpenRead())
            using (var decompressionStream = new GZipStream(originalFileStream, CompressionMode.Decompress))
            using (var textReader = new StreamReader(decompressionStream))
            {
                int count = 0, countAlternatives = 0, skipped = 0;
                string text = null;

                // 1        2           3       4       5           6       7           8
                // titleId	ordering	title	region	language	types	attributes	isOriginalTitle
                // tt0000001	1	Carmencita - spanyol tánc	HU	\N	imdbDisplay	\N	0
                // "tt0033100\t3\tLilla lögnerskan\tSE\t\\N\t\\N\t\\N\t0"
                //                       (1)       2       (3)       (4)       (5)       6       7       8
                var regex = new Regex(@"^([^\t]*)\t[^\t]*\t([^\t]*)\t([^\t]*)\t([^\t]*)\t[^\t]*\t[^\t]*\t[^\t]*$",
                    RegexOptions.Compiled);

                var stopwatch = new Stopwatch();
                stopwatch.Start();

                // Skip header
                textReader.ReadLine();

                var FilterRegion = new string[]
                {
                    "\\N",
                    "BE",
                    "NL",
                    "US",
                    "GB"
                };
                // var FilterLanguage = new string[]
                // {
                //     "en",
                //     "nl",
                // };

                do {

                    using (var db = ImdbDbContextFactory.Create(connectionString))
                    {
                        int batchCount = 0;
                        while (batchCount < 10000 && (text = textReader.ReadLine()) != null)
                        {
                            batchCount++;
                            count++;

                            if (count % 10000 == 0)
                            {                            
                                db.SaveChanges();
                                Console.WriteLine("UpdateImdbDataWithAkas: {1} records done ({0}%), {2} alternatives, {3} records skipped ({4}%), {5}", 
                                    originalFileStream.Position * 100 / originalFileStream.Length, 
                                    count,
                                    countAlternatives, 
                                    skipped, 
                                    skipped * 100 / count,
                                    stopwatch.ElapsedMilliseconds);
                                stopwatch.Restart();                            

                                // For debugging
                                if (debugMaxImdbRowCount > 0 && count >= debugMaxImdbRowCount)
                                    break;
                            }

                            var match = regex.Match(text);
                            if (!match.Success)
                            {
                                Console.WriteLine("Unable to parse line {0}: {1}", count, text);
                                continue;
                            }

                            if (!(FilterRegion.Contains(match.Groups[3].Value) 
                                //|| FilterLanguage.Contains(match.Groups[4].Value)
                                ))
                            {
                                skipped++;
                                continue;
                            }

                            string movieId = match.Groups[1].Value;
                            ImdbDB.Movie movie = db.Movies.Include(m => m.MovieAlternatives).SingleOrDefault(m => m.ImdbId == movieId);
                            if (movie == null)
                            {
                                skipped++;
                                continue;
                            }

                            string alternativeTitle = match.Groups[2].Value;

                            string normalized = ImdbDB.Util.NormalizeTitle(alternativeTitle);
                            // Not in DB
                            if (movie.MovieAlternatives.Any(ma => ma.Normalized == normalized))
                            {
                                skipped++;
                                continue;
                            }

                            var movieAlternative = new MovieAlternative();
                            movieAlternative.Movie = movie;
                            movieAlternative.AlternativeTitle = alternativeTitle;
                            movieAlternative.Normalized = normalized;
                            db.MovieAlternatives.Add(movieAlternative);
                            countAlternatives++;
                        }

                        db.SaveChanges();
                    }

                } while (text != null);

                Console.WriteLine("IMDb movies scanned: {0}", count);
            }
        }

        static void ImportImdbData_Ratings()
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .AddEnvironmentVariables()
                .Build();

            // Get the connection string
            string connectionString = configuration.GetConnectionString("ImdbDb");
            if (!int.TryParse(configuration.GetSection("Grabber")["DebugMaxImdbRowCount"], out int debugMaxImdbRowCount))
                debugMaxImdbRowCount = 0;

            var fileToDecompress = new FileInfo(configuration.GetSection("Grabber")["ImdbRatingsList"]);
            using (var originalFileStream = fileToDecompress.OpenRead())
            using (var decompressionStream = new GZipStream(originalFileStream, CompressionMode.Decompress))
            using (var textReader = new StreamReader(decompressionStream))
            {
                int count = 0, skipped = 0;
                string text = null;

                // New  Distribution  Votes  Rank  Title
                //       0000000125  1852213   9.2  The Shawshank Redemption (1994)
                var regex = new Regex(@"^([^\t]*)\t(\d+\.\d+)\t(\d*)$",
                    RegexOptions.Compiled);

                var stopwatch = new Stopwatch();
                stopwatch.Start();

                // Skip header
                textReader.ReadLine();

                do {

                    using (var db = ImdbDbContextFactory.Create(connectionString))
                    {

                        int batchCount = 0;
                        while (batchCount < 10000 && (text = textReader.ReadLine()) != null)
                        {
                            batchCount++;
                            count++;

                            if (count % 10000 == 0)
                            {
                                db.SaveChanges();
                                Console.WriteLine("UpdateImdbDataWithRatings: {1} records done ({0}%), {2} records skipped, {3}", 
                                    originalFileStream.Position * 100 / originalFileStream.Length, 
                                    count, 
                                    skipped,
                                    stopwatch.ElapsedMilliseconds);
                                stopwatch.Restart();                            

                                // For debugging
                                if (debugMaxImdbRowCount > 0 && count >= debugMaxImdbRowCount)
                                    break;

                            }

                            var match = regex.Match(text);
                            if (!match.Success)
                            {
                                Console.WriteLine("Unable to parse line {0}: {1}", count, text);
                                continue;
                            }

                            string tconst = match.Groups[1].Value;

                            var movie = db.Movies.SingleOrDefault(m => m.ImdbId == tconst);
                            if (movie == null)
                            {
                                // Probably a serie or ...
                                //Console.WriteLine("Unable to find movie {0}", tconst);
                                skipped++;
                                continue;
                            }

                            int votes;
                            if (int.TryParse(match.Groups[3].Value, out votes))
                                movie.Votes = votes;

                            decimal rating;
                            if (decimal.TryParse(match.Groups[2].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture.NumberFormat, out rating))
                                movie.Rating = (int)(rating * 10);
                        }

                        db.SaveChanges();
                    }

                } while (text != null);

                Console.WriteLine("IMDb ratings scanned: {0}", count);
            }
        }

        static void ImportImdbData_CleanupMoviesWithoutRatings()
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            // Get the connection string
            string connectionString = configuration.GetConnectionString("ImdbDb");
            int year = DateTime.Now.Year - 2;


            Console.WriteLine("Removing Movies without Rating");
            do {
                using (var db = ImdbDbContextFactory.Create(connectionString))
                {
                    var batch = db.Movies.Where(m => !m.Rating.HasValue && m.Year <= year).Take(10000).Include(m => m.MovieAlternatives);
                    if (!batch.Any())
                        break;
                    db.MovieAlternatives.RemoveRange(db.Movies.SelectMany(m => m.MovieAlternatives));
                    db.Movies.RemoveRange(batch);
                    db.SaveChanges();
                }
            } while (true);
        }

        static void ImportImdbData_Vacuum()
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            // Get the connection string
            string connectionString = configuration.GetConnectionString("ImdbDb");

            using (var db = ImdbDbContextFactory.Create(connectionString))
            {
                Console.WriteLine("Doing 'VACUUM'...");
                db.Database.ExecuteSqlRaw("VACUUM;");
            }            
        }

        // static void UpdateEpgDataWithImdbManual(int movieEventId, string imdbId)
        // {
        //     var configuration = new ConfigurationBuilder()
        //         .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
        //         .AddEnvironmentVariables()
        //         .Build();

        //     // Get the connection string
        //     string connectionStringMovies = configuration.GetConnectionString("FxMoviesDb");
        //     string connectionStringImdb = configuration.GetConnectionString("ImdbDb");

        //     using (var dbMovies = FxMoviesDbContextFactory.Create(connectionStringMovies))
        //     using (var dbImdb = ImdbDbContextFactory.Create(connectionStringImdb))
        //     {
        //         var movieEvent = dbMovies.MovieEvents.Find(movieEventId);

        //         if (movieEvent == null)
        //         {
        //             Console.WriteLine("UpdateEpgDataWithImdbManual: Unable to find MovieEvent with ID {0}", movieEventId);
        //             return;
        //         }

        //         Console.WriteLine("MovieEvent: {0} ({1}), ID {2}, Current ImdbID={3}", 
        //             movieEvent.Title, movieEvent.Year, movieEvent.Id, movieEvent.Movie.ImdbId);
                    
        //         var movie = dbImdb.Movies.Find(imdbId);

        //         if (movie == null)
        //         {
        //             Console.WriteLine("UpdateEpgDataWithImdbManual: Unable to find IMDb movie with ID {0}", imdbId);
        //             return;
        //         }

        //         Console.WriteLine("IMDb: {0} ({1}), ImdbID={2}", 
        //             movie.PrimaryTitle, movie.Year, movie.ImdbId);

        //         FxMoviesDB.Movie movie = null;
                    
        //         movieEvent.ImdbId = movie.ImdbId;
        //         movieEvent.ImdbRating = movie.Rating;
        //         movieEvent.ImdbVotes = movie.Votes;

        //         movieEvent.Certification = TheMovieDbGrabber.GetCertification(movieEvent.ImdbId) ?? "";

        //         dbMovies.SaveChanges();
        //     }
        // }

        // static void TwitterBot()
        // {
        //     var configuration = new ConfigurationBuilder()
        //         .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
        //         .AddEnvironmentVariables()
        //         .Build();

        //     // Get the connection string
        //     string connectionStringMovies = configuration.GetConnectionString("FxMoviesDb");
        //     string connectionStringImdb = configuration.GetConnectionString("ImdbDb");
        //     string twitterMessageTemplate = configuration.GetSection("Grabber")["TwitterMessageTemplate"];
        //     var twitterChannelHashtags = configuration.GetSection("Grabber").GetSection("TwitterChannelHashtags");

        //     using (var dbMovies = FxMoviesDbContextFactory.Create(connectionStringMovies))
        //     using (var dbImdb = ImdbDbContextFactory.Create(connectionStringImdb))
        //     {
        //         var movieEvent = 
        //             dbMovies.MovieEvents
        //                 .Where(m => m.StartTime >= DateTime.Now.AddHours(1.0) && m.StartTime.TimeOfDay.Hours >= 20)
        //                 .OrderBy(m => m.StartTime.Date)
        //                 .ThenByDescending(m => m.ImdbRating)
        //                 .Include(m => m.Channel)
        //                 .FirstOrDefault();
                
        //         if (movieEvent?.ImdbRating >= 70)
        //         {
        //             System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("nl-BE");
        //             string shortUrl = $"https://filmoptv.be/#{movieEvent.Id}";
        //             string twitterChannelHashtag = twitterChannelHashtags[movieEvent.Channel.Code];
        //             var message = string.Format(
        //                 twitterMessageTemplate,
        //                 movieEvent.Title,
        //                 twitterChannelHashtag ?? movieEvent.Channel.Name,
        //                 movieEvent.StartTime,
        //                 movieEvent.ImdbRating / 10d,
        //                 shortUrl);
        //             message = char.ToUpper(message[0]) + message.Substring(1);
        //             Console.WriteLine("TwitterBot:");
        //             Console.WriteLine(message);
        //         }
        //     }
        // }
    }
}

