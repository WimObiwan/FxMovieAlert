using System;
using System.Collections.Generic;
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
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
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
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .AddEnvironmentVariables()
                    .Build();

                // Get the connection string
                string connectionString = configuration.GetConnectionString("ImdbDb");

                builder.UseSqlite(connectionString); 
                return new ImdbDbContext(builder.Options); 
            }
        }

        static void Main(string[] args)
        {
            // DB Migrations: (removing columns is NOT supported!)
            // cd ./FxMoviesDB/
            // dotnet ef migrations add InitialCreate --startup-project ../Grabber/
            // dotnet ef database update --startup-project ../Grabber
            
            // Minify CSS/JS:
            // cd FxMovieAlert
            // dotnet bundle
            
            // Deploy:
            // dotnet publish -c Release

            // Compact DB:
            // 

            // Generate SQL Script, for production upgrades
            // dotnet ef migrations script --startup-project ../Grabber --context FxMoviesDbContext

            // linux: 
            // http://www.imdb.com/interfaces/
            // https://datasets.imdbws.com/
            // export ConnectionStrings__FxMoviesDb='Data Source=/mnt/data/tmp/fxmovies.db'
            // export ConnectionStrings__ImdbDb='Data Source=/mnt/data/tmp/imdb.db'
            // cd <solutiondir>
            // dotnet ./Grabber/bin/Release/netcoreapp2.0/Grabber.dll GenerateImdbDatabase
            // dotnet ./Grabber/bin/Release/netcoreapp2.0/Grabber.dll UpdateEPG
            // dotnet ./Grabber/bin/Release/netcoreapp2.0/Grabber.dll UpdateImdbUserRatings ur27490911
            // nice -n 16 dotnet ./Grabber/bin/Release/netcoreapp2.0/Grabber.dll UpdateImdbUserRatings ur27490911
            // windows:
            // cd <solutiondir>
            // dotnet build --configuration release
            // dotnet .\Grabber\bin\Release\netcoreapp2.0\Grabber.dll GenerateImdbDatabase
            // dotnet .\Grabber\bin\Release\netcoreapp2.0\Grabber.dll UpdateEPG
            // dotnet .\Grabber\bin\Release\netcoreapp2.0\Grabber.dll UpdateImdbUserRatings ur27490911

            string command = null;
            var arguments = new List<string>();
            foreach (var arg in args)
                if (arg.StartsWith('-'))
                {
                    continue;
                }
                else if (command == null)
                {
                    command = arg;
                }
                else
                {
                    arguments.Add(arg);
                }
            
            if (command == null)
            {                
                // Usage
                Help();

                return;
            }

            if (command.Equals("Help", StringComparison.InvariantCultureIgnoreCase))
            {
                Help();
            } 
            else if (command.Equals("GenerateImdbDatabase"))
            {
                if (arguments.Count != 0)
                {
                    Console.WriteLine("GenerateImdbDatabase: Invalid argument count");
                    return;
                }
                ImportImdbData_Movies();
                ImportImdbData_AlsoKnownAs();
                ImportImdbData_Ratings();
            }
            else if (command.Equals("UpdateImdbUserRatings"))
            {
                if (arguments.Count != 1)
                {
                    Console.WriteLine("Manual: Invalid argument count");
                    return;
                }

                UpdateImdbUserRatings(arguments[0]);
            }
            else if (command.Equals("AutoUpdateImdbUserRatings"))
            {
                if (arguments.Count != 0)
                {
                    Console.WriteLine("Manual: Invalid argument count");
                    return;
                }

                AutoUpdateImdbUserRatings();
            }
            else if (command.Equals("UpdateEPG"))
            {
                if (arguments.Count != 0)
                {
                    Console.WriteLine("UpdateEPG: Invalid argument count");
                    return;
                }
                UpdateDatabaseEpg();
                DownloadImageData();
                UpdateEpgDataWithImdb();
                UpdateDatabaseEpgHistory();
            }
            else if (command.Equals("UpdateVod"))
            {
                UpdateDatabaseVod_YeloPlay();
                UpdateVodDataWithImdb();
            }
            else if (command.Equals("TwitterBot"))
            {
                if (arguments.Count != 0)
                {
                    Console.WriteLine("TwitterBot: Invalid argument count");
                    return;
                }

                TwitterBot();
            }
            else if (command.Equals("Manual"))
            {
                if (arguments.Count != 2)
                {
                    Console.WriteLine("Manual: Invalid argument count");
                    return;
                }

                UpdateEpgDataWithImdbManual(int.Parse(arguments[0]), arguments[1]);
            }
            else
            {
                Console.WriteLine("Unknown command");

                Help();
            }

            // sqlite3 /tmp/imdb.db "VACUUM;" -- 121MB => 103 MB
        }

        static void Help()
        {
            Console.WriteLine("Grabber");
            Console.WriteLine("Usage:");
            Console.WriteLine("   Grabber Help");
            Console.WriteLine("   Grabber GenerateImdbDatabase");
            Console.WriteLine("   Grabber UpdateEPG");
            Console.WriteLine("   Grabber UpdateImdbUserRatings <ImdbUserID(ur...)>");
            Console.WriteLine("   Grabber AutoUpdateImdbUserRatings");
            Console.WriteLine("   Grabber Manual <MovieEventId> <ImdbID(tt...)>");
        }

        static void UpdateImdbUserRatings(string imdbUserId)
        {
            UpdateImdbUserRatings(imdbUserId, false);
            UpdateImdbUserRatings(imdbUserId, true);

            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            // Get the connection string
            string connectionString = configuration.GetConnectionString("FxMoviesDb");

            using (var db = FxMoviesDbContextFactory.Create(connectionString))
            {
                User user = db.Users.Find(imdbUserId);
                if (user == null)
                {
                    user = new User();
                    user.ImdbUserId = imdbUserId;
                    db.Users.Add(user);
                }
                user.RefreshRequestTime = null;
                user.RefreshCount++;

                db.SaveChanges();
            }
        }

        static void UpdateImdbUserRatings(string imdbUserId, bool watchlist)
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            // Get the connection string
            string connectionString = configuration.GetConnectionString("FxMoviesDb");

            Console.WriteLine("Using database: {0}", connectionString);

            var regexImdbId = new Regex(@"/(tt\d+)/", RegexOptions.Compiled);
            var regexRating = new Regex(@"rated this (\d+)\.", RegexOptions.Compiled);
                DateTime now = DateTime.Now;
                string result;
                bool succeeded;

            try
            {
                string suffix = watchlist ? "watchlist" : "ratings";
                string url = $"http://rss.imdb.com/user/{imdbUserId}/{suffix}";
                var request = (HttpWebRequest)WebRequest.Create(url);
                using (var response = request.GetResponse())
                {
                    var xmlDocument = new XmlDocument();
                    xmlDocument.Load(response.GetResponseStream());

                    int count = xmlDocument.DocumentElement["channel"].ChildNodes.Count;
                    string lastDescription = null;
                    DateTime? lastDate = null;

                    foreach (XmlNode item in xmlDocument.DocumentElement["channel"].ChildNodes)
                    {
                        if (item.Name != "item")
                            continue;
                        
                        Console.WriteLine("UpdateImdbUserRatings: {0} - {1} - {2}", item["pubDate"].InnerText, item["title"].InnerText, item["description"].InnerText);

                        string imdbId = regexImdbId.Match(item["link"].InnerText)?.Groups?[1]?.Value;
                        if (imdbId == null)
                            continue;
                        
                        string description = item["description"].InnerText.Trim();

                        DateTime date = DateTime.Parse(item["pubDate"].InnerText, CultureInfo.InvariantCulture.DateTimeFormat);

                        using (var db = FxMoviesDbContextFactory.Create(connectionString))
                        {
                            if (watchlist)
                            {
                                var userWatchListItem = db.UserWatchLists.Find(imdbUserId, imdbId);
                                if (userWatchListItem == null)
                                {
                                    userWatchListItem = new UserWatchListItem();
                                    userWatchListItem.ImdbUserId = imdbUserId;
                                    userWatchListItem.ImdbMovieId = imdbId;
                                    db.UserWatchLists.Add(userWatchListItem);
                                }
                                userWatchListItem.AddedDate = date;

                                Console.WriteLine("UserId={0} IMDbId={1} Added={2}", 
                                    userWatchListItem.ImdbUserId, userWatchListItem.ImdbMovieId, userWatchListItem.AddedDate);
                            }
                            else
                            {
                                string ratingText = regexRating.Match(description)?.Groups?[1]?.Value;
                                if (ratingText == null)
                                    continue;
                                int rating = int.Parse(ratingText);

                                var userRating = db.UserRatings.Find(imdbUserId, imdbId);
                                if (userRating == null)
                                {
                                    userRating = new UserRating();
                                    userRating.ImdbUserId = imdbUserId;
                                    userRating.ImdbMovieId = imdbId;
                                    db.UserRatings.Add(userRating);
                                }
                                userRating.RatingDate = date;
                                userRating.Rating = rating;

                                Console.WriteLine("UserId={0} IMDbId={1} Added={2} Rating={3}", 
                                    userRating.ImdbUserId, userRating.ImdbMovieId, userRating.RatingDate, userRating.Rating);
                            }

                            db.SaveChanges();
                        }

                        if (date > lastDate.GetValueOrDefault(DateTime.MinValue))
                        {
                            lastDate = date;
                            lastDescription = description;
                        }
                    }

                    Console.WriteLine("UpdateImdbUserRatings: Loaded {0} ratings", count);
                    result = string.Format("{0} ratings geladen.", count);
                    if (lastDate.HasValue)
                    {
                        result += string.Format("  Laatste rating gebeurde op {0} (\"{1}\")", lastDate.Value.ToString("yyyy-MM-dd"), lastDescription);
                    }
                    succeeded = true;
                }
            }
            catch (WebException x)
            {
                result = "Foutmelding: " + x.Message;
                succeeded = false;
            }

            using (var db = FxMoviesDbContextFactory.Create(connectionString))
            {
                User user = db.Users.Find(imdbUserId);
                if (user == null)
                {
                    user = new User();
                    user.ImdbUserId = imdbUserId;
                    db.Users.Add(user);
                }
                if (watchlist)
                {
                    user.WatchListLastRefreshTime = DateTime.UtcNow;
                    user.WatchListLastRefreshResult = result;
                    user.WatchListLastRefreshSuccess = succeeded;
                }
                else
                {
                    user.LastRefreshRatingsTime = DateTime.UtcNow;
                    user.LastRefreshRatingsResult = result;
                    user.LastRefreshSuccess = succeeded;
                }

                db.SaveChanges();
            }            
        }

        static void AutoUpdateImdbUserRatings()
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            // Get the connection string
            string connectionString = configuration.GetConnectionString("FxMoviesDb");

            Console.WriteLine("Using database: {0}", connectionString);

            IList<User> users;

            var refreshTime = DateTime.UtcNow.AddDays(-1);
            using (var db = FxMoviesDbContextFactory.Create(connectionString))
            {
                users = db.Users.Where (u => 
                    u.RefreshRequestTime.HasValue || // requested to be refreshed, OR
                    !u.LastRefreshRatingsTime.HasValue || // never refreshed before, OR
                    u.LastRefreshRatingsTime.Value < refreshTime).ToList(); // last refresh is 24 hours ago
            }

            foreach (var user in users)
            {
                Console.WriteLine("User {0} needs a refresh of the IMDb User ratings", user.ImdbUserId);
                if (user.RefreshRequestTime.HasValue)
                    Console.WriteLine("   * RefreshRequestTime = {0} ({1} seconds ago)", 
                        user.RefreshRequestTime.Value, (refreshTime - user.RefreshRequestTime.Value).TotalSeconds);
                if (!user.LastRefreshRatingsTime.HasValue)
                    Console.WriteLine("   * LastRefreshRatingsTime = null");
                else 
                    Console.WriteLine("   * LastRefreshRatingsTime = {0}", 
                        user.LastRefreshRatingsTime.Value);
                    
                UpdateImdbUserRatings(user.ImdbUserId);
            }
        }

        static void UpdateDatabaseEpg()
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            // Get the connection string
            string connectionString = configuration.GetConnectionString("FxMoviesDb");

            var movieTitlesToIgnore = configuration.GetSection("Grabber").GetSection("MovieTitlesToIgnore")
                .GetChildren().Select(i => i.Value).ToList();
            var movieTitlesToTransform = configuration.GetSection("Grabber").GetSection("MovieTitlesToTransform")
                .GetChildren().Select(i => i.Value).ToList();

            Console.WriteLine("Using database: {0}", connectionString);

            DateTime now = DateTime.Now;

            using (var db = FxMoviesDbContextFactory.Create(connectionString))
            {
                // Remove all old MovieEvents
                {
                    var set = db.MovieEvents;
                    set.RemoveRange(set.Where(x => x.StartTime < now.Date));
                }
                db.SaveChanges();
            }

            int maxDays;
            if (!int.TryParse(configuration.GetSection("Grabber")["MaxDays"], out maxDays))
                maxDays = 7;

            for (int days = 0; days <= maxDays; days++)
            {
                DateTime date = now.Date.AddDays(days);
                var movies = HumoGrabber.GetGuide(date).Result;

                YeloGrabber.GetGuide(date, movies);

                // Remove movies that should be ignored
                Func<MovieEvent, bool> isMovieIgnored = delegate(MovieEvent movieEvent)
                {
                    foreach (var item in movieTitlesToIgnore)
                    {
                        if (Regex.IsMatch(movieEvent.Title, item))
                            return true;
                    }
                    return false;
                };
                foreach (var movie in movies.Where(isMovieIgnored))
                {
                    Console.WriteLine("Ignoring movie: {0} {1}", movie.Id, movie.Title);
                }
                movies = movies.Where(m => !isMovieIgnored(m)).ToList();

                // Transform movie titles
                foreach (var movie in movies)
                {
                    foreach (var item in movieTitlesToTransform)
                    {
                        var newTitle = Regex.Replace(movie.Title, item, "$1");
                        var match = Regex.Match(movie.Title, item);
                        if (movie.Title != newTitle)
                        {
                            Console.WriteLine("Transforming movie {0} to {1}", movie.Title, newTitle);
                            movie.Title = newTitle;
                        }
                    }
                }

                Console.WriteLine(date.ToString());
                foreach (var movie in movies)
                {
                    Console.WriteLine("{0} {1} {2} {4}", movie.Channel.Name, movie.Title, movie.Year, movie.Channel.Name, movie.StartTime);
                }

                using (var db = FxMoviesDbContextFactory.Create(connectionString))
                {
                    var existingMovies = db.MovieEvents.Where(x => x.StartTime.Date == date);
                    Console.WriteLine("Existing movies: {0}", existingMovies.Count());
                    Console.WriteLine("New movies: {0}", movies.Count());

                    // Update channels
                    foreach (var channel in movies.Select(m => m.Channel).Distinct())
                    {
                        Channel existingChannel = db.Channels.Find(channel.Code);
                        if (existingChannel != null)
                        {
                            existingChannel.Name = channel.Name;
                            existingChannel.LogoS = channel.LogoS;
                            db.Channels.Update(existingChannel);
                            foreach (var movie in movies.Where(m => m.Channel == channel))
                                movie.Channel = existingChannel;
                        }
                        else
                        {
                            db.Channels.Add(channel);
                        }
                    }

                    // Remove exising movies that don't appear in new movies
                    {
                        var remove = existingMovies.Where(m1 => !movies.Any(m2 => m2.Id == m1.Id));
                        Console.WriteLine("Existing movies to be removed: {0}", remove.Count());
                        db.RemoveRange(remove);
                    }

                    // Update movies
                    foreach (var movie in movies)
                    {
                        var existingMovie = db.MovieEvents.Find(movie.Id);
                        if (existingMovie != null)
                        {
                            if (existingMovie.Title != movie.Title)
                            {
                                existingMovie.Title = movie.Title;
                                existingMovie.ImdbId = null;
                                existingMovie.ImdbRating = null;
                                existingMovie.ImdbVotes = null;
                                existingMovie.Certification = null;
                            }
                            existingMovie.Year = movie.Year;
                            existingMovie.StartTime = movie.StartTime;
                            existingMovie.EndTime = movie.EndTime;
                            existingMovie.Channel = movie.Channel;
                            if (existingMovie.PosterS != movie.PosterS)
                            {
                                existingMovie.PosterS = movie.PosterS;
                                existingMovie.PosterS_Local = null;
                            }
                            if (existingMovie.PosterM != movie.PosterM)
                            {
                                existingMovie.PosterM = movie.PosterM;
                                existingMovie.PosterM_Local = null;
                            }
                            existingMovie.Duration = movie.Duration;
                            existingMovie.Genre = movie.Genre;
                            existingMovie.Content = movie.Content;
                            existingMovie.YeloUrl = movie.YeloUrl;
                        }
                        else
                        {
                            db.MovieEvents.Add(movie);
                        }
                    }

                    // {
                    //     set.RemoveRange(set.Where(x => x.StartTime.Date == date));
                    //     db.SaveChanges();
                    // }

                    db.SaveChanges();
                }
            }
        }

        static void UpdateDatabaseVod_YeloPlay()
        {
            List<VodMovie> vodMovies = YeloPlayGrabber.Get();

            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            // Get the connection string
            string connectionString = configuration.GetConnectionString("FxMoviesDb");

            Console.WriteLine("Using database: {0}", connectionString);

            using (var db = FxMoviesDbContextFactory.Create(connectionString))
            {
                var existingMovies = db.VodMovies.Where(m => YeloPlayGrabber.Provider == m.Provider);
                // Console.WriteLine("Existing movies: {0}", existingMovies.Count());
                // Console.WriteLine("New movies: {0}", movies.Count());

                // Remove exising movies that don't appear in new movies
                {
                    var remove = existingMovies.Where(m1 => 
                        !vodMovies.Any(
                            m2 => m2.Provider == m1.Provider
                            && m2.ProviderCategory == m1.ProviderCategory
                            && m2.ProviderId == m1.ProviderId));
                    Console.WriteLine("Existing movies to be removed: {0}", remove.Count());
                    db.RemoveRange(remove);
                }

                // Update movies
                foreach (var vodMovie in vodMovies)
                {
                    var existingVodMovie = db.VodMovies.Find(vodMovie.Provider, vodMovie.ProviderCategory, vodMovie.ProviderId);
                    if (existingVodMovie != null)
                    {
                        if (existingVodMovie.Title != vodMovie.Title)
                        {
                            existingVodMovie.Title = vodMovie.Title;
                            existingVodMovie.ImdbId = null;
                            existingVodMovie.ImdbRating = null;
                            existingVodMovie.ImdbVotes = null;
                            existingVodMovie.Certification = null;
                        }
                        if (existingVodMovie.Image != vodMovie.Image)
                        {
                            existingVodMovie.Image = vodMovie.Image;
                            existingVodMovie.Image_Local = null;
                        }
                        existingVodMovie.ProviderMask = vodMovie.ProviderMask;
                        existingVodMovie.Price = vodMovie.Price;
                        existingVodMovie.ValidFrom = vodMovie.ValidFrom;
                        existingVodMovie.ValidUntil = vodMovie.ValidUntil;
                    }
                    else
                    {
                        db.VodMovies.Add(vodMovie);
                    }
                }

                // {
                //     set.RemoveRange(set.Where(x => x.StartTime.Date == date));
                //     db.SaveChanges();
                // }

                db.SaveChanges();
            }
        }

        static void UpdateDatabaseEpgHistory()
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            // Get the connection string
            string connectionString = configuration.GetConnectionString("FxMoviesDb");
            string connectionStringHistory = configuration.GetConnectionString("FxMoviesHistoryDb");

            Console.WriteLine("Using database: {0}", connectionString);
            Console.WriteLine("Using database: {0}", connectionStringHistory);

            using (var db = FxMoviesDbContextFactory.Create(connectionString))
            using (var dbHistory = FxMoviesDbContextFactory.Create(connectionStringHistory))
            {
                foreach (var channel in db.Channels)
                {
                    var channelHistory = dbHistory.Channels.Find(channel.Code);
                    if (channelHistory == null)
                    {
                        channelHistory = new Channel();
                        channelHistory.Code = channel.Code;
                        dbHistory.Channels.Add(channelHistory);
                    }
                    channelHistory.Name = channel.Name;
                    channelHistory.LogoS = channel.LogoS;
                    channelHistory.LogoS_Local = channel.LogoS_Local;
                }
                dbHistory.SaveChanges();

                var min = db.MovieEvents.Where(i => i.StartTime >= DateTime.Now).Select(i => i.StartTime).Min();
                var movieEventsToRemove = dbHistory.MovieEvents.Where(i => i.StartTime >= min);
                dbHistory.MovieEvents.RemoveRange(movieEventsToRemove);
                int lastId = dbHistory.MovieEvents.Select(i => i.Id).DefaultIfEmpty(0).Max();
                foreach (var movieEvent in db.MovieEvents)
                {
                    var movieEventHistory = dbHistory.MovieEvents.Find(movieEvent.Id);
                    if (movieEventHistory != null)
                        dbHistory.MovieEvents.Remove(movieEventHistory);

                    var channelHistory = dbHistory.Channels.Find(movieEvent.Channel.Code);

                    movieEventHistory = new MovieEvent();
                    movieEventHistory.Id = ++lastId;
                    movieEventHistory.Title = movieEvent.Title;
                    movieEventHistory.Year = movieEvent.Year;
                    movieEventHistory.StartTime = movieEvent.StartTime;
                    movieEventHistory.EndTime = movieEvent.EndTime;
                    movieEventHistory.Channel = channelHistory;
                    movieEventHistory.PosterS = movieEvent.PosterS;
                    movieEventHistory.PosterM = movieEvent.PosterM;
                    movieEventHistory.Duration = movieEvent.Duration;
                    movieEventHistory.Genre = movieEvent.Genre;
                    movieEventHistory.Content = movieEvent.Content;
                    movieEventHistory.ImdbId = movieEvent.ImdbId;
                    movieEventHistory.ImdbRating = movieEvent.ImdbRating;
                    movieEventHistory.ImdbVotes = movieEvent.ImdbVotes;
                    movieEventHistory.YeloUrl = null;
                    movieEventHistory.Certification = movieEvent.Certification;
                    movieEventHistory.PosterS_Local = movieEvent.PosterS_Local;
                    movieEventHistory.PosterM_Local = movieEvent.PosterM_Local;
                    dbHistory.MovieEvents.Add(movieEventHistory);
                }
                dbHistory.SaveChanges();
            }
        }

        static void DownloadImageData()
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();
            
            // Get the connection string
            string connectionStringMovies = configuration.GetConnectionString("FxMoviesDb");
            string basePath = configuration.GetSection("Grabber")["ImageBasePath"];
            if (!Directory.Exists(basePath))
                Directory.CreateDirectory(basePath);

            using (var dbMovies = FxMoviesDbContextFactory.Create(connectionStringMovies))
            {
                foreach (var channel in dbMovies.Channels)
                {
                    string url = channel.LogoS;

                    string ext;
                    int extStart = url.LastIndexOf('.');
                    if (extStart == -1)
                        ext = ".jpg";
                    else
                        ext = url.Substring(extStart);

                    string name = "channel-" + channel.Code + ext;
                    string target = Path.Combine(basePath, name);

                    // bool reset = false;
                    
                    // if (name != channel.LogoS_Local)
                    // {
                        channel.LogoS_Local = name;
                    //     reset = true;
                    // }
                    // else if (!File.Exists(target))
                    // {
                    //     reset = true;
                    // }

                    // string eTag = reset ? null : channel.LogoS_ETag;
                    // DateTime? lastModified = reset ? null : channel.LogoS_LastModified;

                    DownloadFile(url, target /*, ref eTag, ref lastModified*/);

                    // channel.LogoS_ETag = eTag;
                    // channel.LogoS_LastModified = lastModified;
                }

                foreach (var movieEvent in dbMovies.MovieEvents)
                {
                    {
                        string url = movieEvent.PosterS;

                        if (url == null)
                            continue;

                        string ext;
                        int extStart = url.LastIndexOf('.');
                        if (extStart == -1)
                            ext = ".jpg";
                        else
                            ext = url.Substring(extStart);

                        string name = "movie-" + movieEvent.Id.ToString() + "-S" + ext;
                        string target = Path.Combine(basePath, name);

                        movieEvent.PosterS_Local = name;

                        DownloadFile(url, target /*, ref eTag, ref lastModified*/);
                    }
                    {
                        string url = movieEvent.PosterM;

                        if (url == null)
                            continue;

                        string ext;
                        int extStart = url.LastIndexOf('.');
                        if (extStart == -1)
                            ext = ".jpg";
                        else
                            ext = url.Substring(extStart);

                        string name = "movie-" + movieEvent.Id.ToString() + "-M" + ext;
                        string target = Path.Combine(basePath, name);

                        movieEvent.PosterM_Local = name;

                        DownloadFile(url, target /*, ref eTag, ref lastModified*/);
                    }
                }

                dbMovies.SaveChanges();    
            }
        }

        static void DownloadFile(string url, string target)
        {
            Console.WriteLine($"Downloading {url} to {target}");
            var req = (HttpWebRequest)WebRequest.Create(url);
            using (var rsp = (HttpWebResponse)req.GetResponse())
            {
                using (var stm = rsp.GetResponseStream())
                using (var fileStream = File.Create(target))
                {
                    stm.CopyTo(fileStream);
                }
            }
        }

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

        static void UpdateEpgDataWithImdb()
        {
            UpdateGenericDataWithImdb<MovieEvent>((dbMovies) => dbMovies.MovieEvents);
        }

        static void UpdateVodDataWithImdb()
        {
            UpdateGenericDataWithImdb<VodMovie>((dbMovies) => dbMovies.VodMovies);
        }

        static void UpdateGenericDataWithImdb<T>(Func<FxMoviesDbContext, IQueryable<IHasImdbLink>> fnGetMovies) 
        where T : IHasImdbLink
        {
            // aws s3api get-object --request-payer requester --bucket imdb-datasets --key documents/v1/current/title.basics.tsv.gz title.basics.tsv.gz
            // aws s3api get-object --request-payer requester --bucket imdb-datasets --key documents/v1/current/title.ratings.tsv.gz title.ratings.tsv.gz
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            // Get the connection string
            string connectionStringMovies = configuration.GetConnectionString("FxMoviesDb");
            string connectionStringImdb = configuration.GetConnectionString("ImdbDb");

            int imdbHuntingYearDiff = int.Parse(configuration.GetSection("Grabber")["ImdbHuntingYearDiff"]);

            using (var dbMovies = FxMoviesDbContextFactory.Create(connectionStringMovies))
            using (var dbImdb = ImdbDbContextFactory.Create(connectionStringImdb))
            {
                var huntingProcedure = new List<object>();

                // Search for PrimaryTitle (Year)
                huntingProcedure.Add((Func<IHasImdbLink, Movie, bool>)
                (
                    (movieWithImdbLink, m) => m.PrimaryTitle.Equals(movieWithImdbLink.Title, StringComparison.InvariantCultureIgnoreCase) 
                                && (!m.Year.HasValue || !movieWithImdbLink.Year.HasValue || m.Year == movieWithImdbLink.Year)
                ));

                // Search for AlternativeTitle (Year)
                huntingProcedure.Add(Tuple.Create(
                    (Func<IHasImdbLink, MovieAlternative, bool>)(
                        (movieWithImdbLink, ma) => ma.AlternativeTitle.Equals(movieWithImdbLink.Title, StringComparison.InvariantCultureIgnoreCase)
                    ),
                    (Func<IHasImdbLink, Movie, bool>)(
                        (movieWithImdbLink, m) => !m.Year.HasValue || !movieWithImdbLink.Year.HasValue || m.Year == movieWithImdbLink.Year
                    )
                ));

                // Search for PrimaryTitle (+/-Year)
                huntingProcedure.Add((Func<IHasImdbLink, Movie, bool>)
                (
                    (movieWithImdbLink, m) => m.PrimaryTitle.Equals(movieWithImdbLink.Title, StringComparison.InvariantCultureIgnoreCase) 
                                && (!m.Year.HasValue || !movieWithImdbLink.Year.HasValue || (m.Year >= movieWithImdbLink.Year - imdbHuntingYearDiff) && (m.Year <= movieWithImdbLink.Year + imdbHuntingYearDiff))
                ));

                // Search for AlternativeTitle (+/-Year)
                huntingProcedure.Add(Tuple.Create(
                    (Func<IHasImdbLink, MovieAlternative, bool>)(
                        (movieWithImdbLink, ma) => ma.AlternativeTitle.Equals(movieWithImdbLink.Title, StringComparison.InvariantCultureIgnoreCase)
                    ),
                    (Func<IHasImdbLink, Movie, bool>)(
                        (movieWithImdbLink, m) => !m.Year.HasValue || !movieWithImdbLink.Year.HasValue || (m.Year >= movieWithImdbLink.Year - imdbHuntingYearDiff) && (m.Year <= movieWithImdbLink.Year + imdbHuntingYearDiff)
                    )
                ));

                var groups = fnGetMovies(dbMovies).GroupBy(m => new { m.Title, m.Year });
                int totalCount = groups.Count();
                int current = 0;
                foreach (var group in groups) //.ToList())
                {
                    if (group.Any(m => m.ImdbId != null))
                    {
                        var first = group.First(m => m.ImdbId != null);
                        foreach (var other in group.Where(m => m.ImdbId == null))
                        {
                            other.ImdbId = first.ImdbId;
                            other.ImdbVotes = first.ImdbVotes;
                            other.ImdbRating = first.ImdbRating;
                            other.Certification = first.Certification;
                        }
                        continue;
                    }

                    var firstMovieWithImdbLink = group.First(m => m.ImdbId == null);

                    Movie movie;
                    if (firstMovieWithImdbLink.ImdbId != null)
                    {
                        movie = dbImdb.Movies.Find(firstMovieWithImdbLink.ImdbId);
                    }
                    else
                    {
                        movie = null;
                        foreach (var hunt in huntingProcedure)
                        {
                            if (hunt is Func<IHasImdbLink, Movie, bool> huntTyped1)
                            {
                                movie = dbImdb.Movies
                                    .Where(m => huntTyped1(firstMovieWithImdbLink, m))
                                    .OrderByDescending(m => m.Votes)
                                    .FirstOrDefault();
                            }
                            else if (hunt is Tuple<Func<IHasImdbLink, MovieAlternative, bool>, Func<IHasImdbLink, Movie, bool>> huntTyped2)
                            {
                                var movieAlternatives = dbImdb.MovieAlternatives.Where(m => huntTyped2.Item1(firstMovieWithImdbLink, m));
                                movie = dbImdb.Movies.Join(movieAlternatives, m => m.Id, ma => ma.Id, (m, ma) => m)
                                    .Where(m => huntTyped2.Item2(firstMovieWithImdbLink, m))
                                    .OrderByDescending(m => m.Votes)
                                    .FirstOrDefault();
                            }
                            else
                            {
                                throw new InvalidOperationException($"Unknown hunt type {hunt}");
                            }

                            if (movie != null)
                                break;
                        }
                    }
                    
                    if (movie == null)
                    {
                        foreach (var movieWithImdbLink in group)
                        {
                            movieWithImdbLink.ImdbId = "";
                        }
                        dbMovies.SaveChanges();
                        Console.WriteLine("UpdateEpgDataWithImdb: Could not find movie '{0} ({1})' in IMDb", firstMovieWithImdbLink.Title, firstMovieWithImdbLink.Year);
                        continue;
                    }

                    Console.WriteLine("{3}% {0} ({1}) ==> {2}, duplicity={4}", 
                        firstMovieWithImdbLink.Title, 
                        firstMovieWithImdbLink.Year, 
                        movie.Id,
                        (100 * current++) / totalCount,
                        group.Count());

                    foreach (var movieWithImdbLink in group)
                    {
                        if (movieWithImdbLink.ImdbId != movie.Id)
                            movieWithImdbLink.Certification = null;
                        movieWithImdbLink.ImdbId = movie.Id;
                        movieWithImdbLink.ImdbRating = movie.Rating;
                        movieWithImdbLink.ImdbVotes = movie.Votes;
                        
                        if (movieWithImdbLink.Certification == null)
                            movieWithImdbLink.Certification = TheMovieDbGrabber.GetCertification(movieWithImdbLink.ImdbId) ?? "";
                    }

                    dbMovies.SaveChanges();
                }

                dbMovies.SaveChanges();
            }
        }

        static void ImportImdbData_Movies()
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            // Get the connection string
            string connectionString = configuration.GetConnectionString("ImdbDb");

            using (var db = ImdbDbContextFactory.Create(connectionString))
            {
                db.Movies.RemoveRange(db.Movies);
                db.MovieAlternatives.RemoveRange(db.MovieAlternatives);
                db.SaveChanges();

                var fileToDecompress = new FileInfo(configuration.GetSection("Grabber")["ImdbMoviesList"]);
                using (var originalFileStream = fileToDecompress.OpenRead())
                using (var decompressionStream = new GZipStream(originalFileStream, CompressionMode.Decompress))
                using (var textReader = new StreamReader(decompressionStream))
                {
                    int count = 0, countAlternatives = 0, skipped = 0;
                    string text;

                    // tconst	titleType	primaryTitle	originalTitle	isAdult	startYear	endYear	runtimeMinutes	genres
                    // tt0000009	movie	Miss Jerry	Miss Jerry	0	1894	\N	45	Romance
                    var regex = new Regex(@"^([^\t]*)\t([^\t]*)\t([^\t]*)\t([^\t]*)\t[^\t]*\t([^\t]*)\t[^\t]*\t[^\t]*\t[^\t]*$",
                        RegexOptions.Compiled);

                    // Skip header
                    textReader.ReadLine();

                    var FilterTypes = new string[]
                    {
                        "movie",
                        "video",
                        "short",
                        "tvMovie",
                        "tvMiniSeries",
                    };

                    while ((text = textReader.ReadLine()) != null)
                    {
                        count++;

                        if (count % 10000 == 0)
                        {
                            Console.WriteLine("UpdateImdbDataWithMovies: {1} records done ({0}%), {2} alternatives, {3} records skipped ({4}%)", 
                                originalFileStream.Position * 100 / originalFileStream.Length, 
                                count,
                                countAlternatives, 
                                skipped, 
                                skipped * 100 / count);
                            db.SaveChanges();
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
                        movie.Id = movieId;
                        movie.PrimaryTitle = match.Groups[3].Value;
                        string originalTitle = match.Groups[4].Value;

                        if (int.TryParse(match.Groups[5].Value, out int startYear))
                            movie.Year = startYear;

                        db.Movies.Add(movie);

                        if (!string.Equals(movie.PrimaryTitle, originalTitle, StringComparison.InvariantCultureIgnoreCase))
                        {
                            countAlternatives++;
                            var movieAlternative = new MovieAlternative();
                            movieAlternative.Id = movieId;
                            movieAlternative.AlternativeTitle = originalTitle;
                            db.MovieAlternatives.Add(movieAlternative);
                        }
                    }

                    Console.WriteLine("IMDb movies scanned: {0}", count);
                }

                db.SaveChanges();
            }
        }        

        static void ImportImdbData_AlsoKnownAs()
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            // Get the connection string
            string connectionString = configuration.GetConnectionString("ImdbDb");

            using (var db = ImdbDbContextFactory.Create(connectionString))
            {
                var fileToDecompress = new FileInfo(configuration.GetSection("Grabber")["ImdbAlsoKnownAsList"]);
                using (var originalFileStream = fileToDecompress.OpenRead())
                using (var decompressionStream = new GZipStream(originalFileStream, CompressionMode.Decompress))
                using (var textReader = new StreamReader(decompressionStream))
                {
                    int count = 0, countAlternatives = 0, skipped = 0;
                    string text;

                    // 1        2           3       4       5           6       7           8
                    // titleId	ordering	title	region	language	types	attributes	isOriginalTitle
                    // tt0000001	1	Carmencita - spanyol tánc	HU	\N	imdbDisplay	\N	0
                    // "tt0033100\t3\tLilla lögnerskan\tSE\t\\N\t\\N\t\\N\t0"
                    //                       (1)       2       (3)       (4)       (5)       6       7       8
                    var regex = new Regex(@"^([^\t]*)\t[^\t]*\t([^\t]*)\t([^\t]*)\t([^\t]*)\t[^\t]*\t[^\t]*\t[^\t]*$",
                        RegexOptions.Compiled);

                    // Skip header
                    textReader.ReadLine();

                    var FilterRegion = new string[]
                    {
                        "BE",
                        "NL",
                    };
                    // var FilterLanguage = new string[]
                    // {
                    // };

                    string lastMovieId = null;
                    List<string> lastMovieAlternativesToAdd = new List<string>();
                    IQueryable<MovieAlternative> lastMovieAlternatives = null;

                    while ((text = textReader.ReadLine()) != null)
                    {
                        count++;

                        if (count % 10000 == 0)
                        {
                            Console.WriteLine("UpdateImdbDataWithAkas: {1} records done ({0}%), {2} alternatives, {3} records skipped ({4}%)", 
                                originalFileStream.Position * 100 / originalFileStream.Length, 
                                count,
                                countAlternatives, 
                                skipped, 
                                skipped * 100 / count);
                            db.SaveChanges();
                        }

                        var match = regex.Match(text);
                        if (!match.Success)
                        {
                            Console.WriteLine("Unable to parse line {0}: {1}", count, text);
                            continue;
                        }

                        // Filter on movie|video|short|tvMovie|tvMiniSeries
                        if (!FilterRegion.Contains(match.Groups[3].Value)
                            // && !FilterLanguage.Contains(match.Groups[4].Value)
                            )
                        {
                            skipped++;
                            continue;
                        }

                        string movieId = match.Groups[1].Value;
                        Movie movie = db.Movies.Find(movieId);
                        if (movie == null)
                        {
                            skipped++;
                            continue;
                        }

                        string alternativeTitle = match.Groups[2].Value;

                        if (movieId != lastMovieId)
                        {
                            if (lastMovieId != null)
                            {   
                                AddMovieAlternatives(db, lastMovieId, lastMovieAlternatives.Select(ma => ma.No).DefaultIfEmpty(0).Max(),
                                    lastMovieAlternativesToAdd);
                            }

                            lastMovieId = movieId;
                            lastMovieAlternativesToAdd.Clear();
                            lastMovieAlternatives = db.MovieAlternatives.Where(ma => ma.Id == movieId);
                        }

                        if (!lastMovieAlternatives.Any(ma => string.Equals(ma.AlternativeTitle, alternativeTitle, StringComparison.InvariantCultureIgnoreCase)))
                        {
                            lastMovieAlternativesToAdd.Add(alternativeTitle);
                            countAlternatives++;
                        }
                    }

                    AddMovieAlternatives(db, lastMovieId, lastMovieAlternatives.Select(ma => ma.No).DefaultIfEmpty(0).Max(),
                        lastMovieAlternativesToAdd);

                    Console.WriteLine("IMDb movies scanned: {0}", count);
                }

                db.SaveChanges();
            }
        }

        static void AddMovieAlternatives(ImdbDbContext db, string movieId, int lastNo, List<string> alternativesToAdd)
        {
            foreach (string alternativeTitle in alternativesToAdd)
            {
                var movieAlternative = new MovieAlternative();
                movieAlternative.Id = movieId;
                movieAlternative.No = ++lastNo;
                movieAlternative.AlternativeTitle = alternativeTitle;
                db.MovieAlternatives.Add(movieAlternative);
                // db.SaveChangesAsync();
            }
        }

        static void ImportImdbData_Ratings()
        {
            // aws s3api get-object --request-payer requester --bucket imdb-datasets --key documents/v1/current/title.basics.tsv.gz title.basics.tsv.gz
            // aws s3api get-object --request-payer requester --bucket imdb-datasets --key documents/v1/current/title.ratings.tsv.gz title.ratings.tsv.gz

            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            // Get the connection string
            string connectionString = configuration.GetConnectionString("ImdbDb");

            using (var db = ImdbDbContextFactory.Create(connectionString))
            {
                var fileToDecompress = new FileInfo(configuration.GetSection("Grabber")["ImdbRatingsList"]);
                using (var originalFileStream = fileToDecompress.OpenRead())
                using (var decompressionStream = new GZipStream(originalFileStream, CompressionMode.Decompress))
                using (var textReader = new StreamReader(decompressionStream))
                {
                    int count = 0, skipped = 0;
                    string text;

                    // New  Distribution  Votes  Rank  Title
                    //       0000000125  1852213   9.2  The Shawshank Redemption (1994)
                    var regex = new Regex(@"^([^\t]*)\t(\d+\.\d+)\t(\d*)$",
                        RegexOptions.Compiled);

                    // Skip header
                    textReader.ReadLine();

                    while ((text = textReader.ReadLine()) != null)
                    {
                        count++;

                        if (count % 10000 == 0)
                        {
                            Console.WriteLine("UpdateImdbDataWithRatings: {1} records done ({0}%), {2} records skipped", 
                                originalFileStream.Position * 100 / originalFileStream.Length, 
                                count, 
                                skipped,
                                skipped * 100 / count);
                            db.SaveChanges();
                        }

                        var match = regex.Match(text);
                        if (!match.Success)
                        {
                            Console.WriteLine("Unable to parse line {0}: {1}", count, text);
                            continue;
                        }

                        string tconst = match.Groups[1].Value;

                        var movie = db.Movies.Find(tconst);
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

                    Console.WriteLine("IMDb ratings scanned: {0}", count);
                }

                db.SaveChanges();
            }
        }
        
        static void UpdateEpgDataWithImdbManual(int movieEventId, string imdbId)
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            // Get the connection string
            string connectionStringMovies = configuration.GetConnectionString("FxMoviesDb");
            string connectionStringImdb = configuration.GetConnectionString("ImdbDb");

            using (var dbMovies = FxMoviesDbContextFactory.Create(connectionStringMovies))
            using (var dbImdb = ImdbDbContextFactory.Create(connectionStringImdb))
            {
                var movieEvent = dbMovies.MovieEvents.Find(movieEventId);

                if (movieEvent == null)
                {
                    Console.WriteLine("UpdateEpgDataWithImdbManual: Unable to find MovieEvent with ID {0}", movieEventId);
                    return;
                }

                Console.WriteLine("MovieEvent: {0} ({1}), ID {2}, Current ImdbID={3}", 
                    movieEvent.Title, movieEvent.Year, movieEvent.Id, movieEvent.ImdbId);
                    
                var movie = dbImdb.Movies.Find(imdbId);

                if (movie == null)
                {
                    Console.WriteLine("UpdateEpgDataWithImdbManual: Unable to find IMDb movie with ID {0}", imdbId);
                    return;
                }

                Console.WriteLine("IMDb: {0} ({1}), ImdbID={2}", 
                    movie.PrimaryTitle, movie.Year, movie.Id);
                    
                movieEvent.ImdbId = movie.Id;
                movieEvent.ImdbRating = movie.Rating;
                movieEvent.ImdbVotes = movie.Votes;

                movieEvent.Certification = TheMovieDbGrabber.GetCertification(movieEvent.ImdbId) ?? "";

                dbMovies.SaveChanges();
            }
        }

        static void TwitterBot()
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            // Get the connection string
            string connectionStringMovies = configuration.GetConnectionString("FxMoviesDb");
            string connectionStringImdb = configuration.GetConnectionString("ImdbDb");
            string twitterMessageTemplate = configuration.GetSection("Grabber")["TwitterMessageTemplate"];
            var twitterChannelHashtags = configuration.GetSection("Grabber").GetSection("TwitterChannelHashtags");

            using (var dbMovies = FxMoviesDbContextFactory.Create(connectionStringMovies))
            using (var dbImdb = ImdbDbContextFactory.Create(connectionStringImdb))
            {
                var movieEvent = 
                    dbMovies.MovieEvents
                        .Where(m => m.StartTime >= DateTime.Now.AddHours(1.0) && m.StartTime.TimeOfDay.Hours >= 20)
                        .OrderBy(m => m.StartTime.Date)
                        .ThenByDescending(m => m.ImdbRating)
                        .Include(m => m.Channel)
                        .FirstOrDefault();
                
                if (movieEvent?.ImdbRating >= 70)
                {
                    System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("nl-BE");
                    string shortUrl = $"https://filmoptv.be/#{movieEvent.Id}";
                    string twitterChannelHashtag = twitterChannelHashtags[movieEvent.Channel.Code];
                    var message = string.Format(
                        twitterMessageTemplate,
                        movieEvent.Title,
                        twitterChannelHashtag ?? movieEvent.Channel.Name,
                        movieEvent.StartTime,
                        movieEvent.ImdbRating / 10d,
                        shortUrl);
                    message = char.ToUpper(message[0]) + message.Substring(1);
                    Console.WriteLine("TwitterBot:");
                    Console.WriteLine(message);
                }
            }
        }
    }
}

