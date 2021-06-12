using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using FxMovies.FxMoviesDB;
using FxMovies.ImdbDB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FxMovies.Core
{
    public interface IUpdateEpgCommand
    {
        int Run();
    }

    public class UpdateEpgCommandOptions
    {
        public static string Position => "UpdateEpg";

        public string[] MovieTitlesToIgnore { get; set; }
        public string[] MovieTitlesToTransform { get; set; }
        public int? MaxDays { get; set; }
        public string ImageBasePath { get; set; }
        public int? ImdbHuntingYearDiff { get; set; }
        public Dictionary<string, string> ImageOverrideMap { get; set; }
    }

    public class UpdateEpgCommand : IUpdateEpgCommand
    {
        private readonly ILogger<UpdateEpgCommand> logger;
        private readonly IDbContextFactory<FxMoviesDbContext> fxMoviesDbContextFactory;
        private readonly IDbContextFactory<ImdbDbContext> imdbDbContextFactory;
        private readonly UpdateEpgCommandOptions updateEpgCommandOptions;
        private readonly ITheMovieDbService theMovieDbService;
        private readonly IHumoService humoService;

        public UpdateEpgCommand(ILogger<UpdateEpgCommand> logger, 
            IDbContextFactory<FxMoviesDbContext> fxMoviesDbContextFactory, IDbContextFactory<ImdbDbContext> imdbDbContextFactory,
            IOptionsSnapshot<UpdateEpgCommandOptions> updateEpgCommandOptions,
            ITheMovieDbService theMovieDbService,
            IHumoService humoService)
        {
            this.logger = logger;
            this.fxMoviesDbContextFactory = fxMoviesDbContextFactory;
            this.imdbDbContextFactory = imdbDbContextFactory;
            this.updateEpgCommandOptions = updateEpgCommandOptions.Value;
            this.theMovieDbService = theMovieDbService;
            this.humoService = humoService;
        }

        public int Run()
        {
            UpdateDatabaseEpg();
            UpdateMissingImageLinks();
            DownloadImageData();
            UpdateEpgDataWithImdb();
            //UpdateDatabaseEpgHistory();
            return 0;
        }

        private void UpdateDatabaseEpg()
        {
            DateTime now = DateTime.Now;

            using (var db = fxMoviesDbContextFactory.CreateDbContext())
            {
                // Remove all old MovieEvents
                {
                    var set = db.MovieEvents;
                    set.RemoveRange(set.Where(x => x.StartTime < now.Date));
                }
                db.SaveChanges();
            }

            int maxDays = updateEpgCommandOptions.MaxDays ?? 7;
            for (int days = 0; days <= maxDays; days++)
            {
                DateTime date = now.Date.AddDays(days);
                var movies = humoService.GetGuide(date).Result;

                //YeloGrabber.GetGuide(date, movies);

                // Remove movies that should be ignored
                Func<MovieEvent, bool> isMovieIgnored = delegate(MovieEvent movieEvent)
                {
                    foreach (var item in updateEpgCommandOptions.MovieTitlesToIgnore)
                    {
                        if (Regex.IsMatch(movieEvent.Title, item))
                            return true;
                    }
                    return false;
                };
                foreach (var movie in movies.Where(isMovieIgnored))
                {
                    logger.LogInformation($"Ignoring movie: {movie.Id} {movie.Title}");
                }
                movies = movies.Where(m => !isMovieIgnored(m)).ToList();

                // Transform movie titles
                foreach (var movie in movies)
                {
                    foreach (var item in updateEpgCommandOptions.MovieTitlesToTransform)
                    {
                        var newTitle = Regex.Replace(movie.Title, item, "$1");
                        var match = Regex.Match(movie.Title, item);
                        if (movie.Title != newTitle)
                        {
                            logger.LogInformation($"Transforming movie {movie.Title} to {newTitle}");
                            movie.Title = newTitle;
                        }
                    }
                }

                logger.LogInformation(date.ToString());
                foreach (var movie in movies)
                {
                    logger.LogInformation($"{movie.Channel.Name} {movie.Title} {movie.Year} {movie.StartTime}");
                }

                using (var db = fxMoviesDbContextFactory.CreateDbContext())
                {
                    var existingMovies = db.MovieEvents.Where(x => x.StartTime.Date == date);
                    logger.LogInformation("Existing movies: {0}", existingMovies.Count());
                    logger.LogInformation("New movies: {0}", movies.Count());

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
                        var remove = existingMovies.ToList().Where(m1 => !movies.Any(m2 => m2.Id == m1.Id));
                        logger.LogInformation("Existing movies to be removed: {0}", remove.Count());
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
                                existingMovie.Movie = null;
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
                            existingMovie.Opinion = movie.Opinion;
                            existingMovie.Type = movie.Type;
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

            using (var db = fxMoviesDbContextFactory.CreateDbContext())
            {
                // Remove all old MovieEvents
                {
                    var set = db.Channels.Where(ch => db.MovieEvents.All(me => me.Channel != ch));
                    db.RemoveRange(set);
                }
                db.SaveChanges();
            }
        }

        private void UpdateMissingImageLinks()
        {
            using (var db = fxMoviesDbContextFactory.CreateDbContext())
            {
                foreach (var movieEvent in db.MovieEvents)
                {
                    bool emptyPosterM = string.IsNullOrEmpty(movieEvent.PosterM);
                    bool emptyPosterS = string.IsNullOrEmpty(movieEvent.PosterS);
                    if ((emptyPosterM || emptyPosterS) && !string.IsNullOrEmpty(movieEvent.Movie?.ImdbId))
                    {
                        var result = theMovieDbService.GetImages(movieEvent.Movie.ImdbId);
                        movieEvent.PosterM = result.Medium;
                        movieEvent.PosterS = result.Small;
                    }
                }
                db.SaveChanges();
            }
        }

        private void DownloadImageData()
        {
            string basePath = updateEpgCommandOptions.ImageBasePath;
            if (!Directory.Exists(basePath))
                Directory.CreateDirectory(basePath);

            using (var dbMovies = fxMoviesDbContextFactory.CreateDbContext())
            {
                foreach (var channel in dbMovies.Channels)
                {
                    string url = channel.LogoS;

                    if (url == null)
                        continue;

                    string ext = ".jpg";

                    if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
                    {
                        string path = uri.PathAndQuery;
                        int extStart = path.LastIndexOf('.');
                        if (extStart != -1)
                            ext = url.Substring(extStart);
                    }

                    string nameWithoutExt = "channel-" + channel.Code;
                    string name = nameWithoutExt + ext;

                    if (updateEpgCommandOptions.ImageOverrideMap.TryGetValue(nameWithoutExt, out string imageOverride))
                    {
                        string target = Path.Combine(basePath, name);
                        File.Copy(imageOverride, target, true);
                    }
                    else
                    {
                        // bool reset = false;
                        
                        // if (name != channel.LogoS_Local)
                        // {
                        //    channel.LogoS_Local = name;
                        //     reset = true;
                        // }
                        // else if (!File.Exists(target))
                        // {
                        //     reset = true;
                        // }

                        // string eTag = reset ? null : channel.LogoS_ETag;
                        // DateTime? lastModified = reset ? null : channel.LogoS_LastModified;
                        //
                        
                        // Resize to 50 gives black background on Vier, Vijf, ...
                        channel.LogoS_Local = DownloadFile(url, basePath, name, 0);

                        // channel.LogoS_ETag = eTag;
                        // channel.LogoS_LastModified = lastModified;
                    }
                }

                foreach (var movieEvent in dbMovies.MovieEvents)
                {
                    {
                        string url = movieEvent.PosterS;

                        if (url == null)
                            continue;

                        string ext = ".jpg";
                        if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
                        {
                            string path = uri.PathAndQuery;
                            int extStart = path.LastIndexOf('.');
                            if (extStart != -1)
                                ext = url.Substring(extStart);
                        }

                        string name = "movie-" + movieEvent.Id.ToString() + "-S" + ext;

                        movieEvent.PosterS_Local = DownloadFile(url, basePath, name, 150);
                    }
                    {
                        string url = movieEvent.PosterM;

                        if (url == null)
                            continue;

                        string ext = ".jpg";
                        if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
                        {
                            string path = uri.PathAndQuery;
                            int extStart = path.LastIndexOf('.');
                            if (extStart != -1)
                                ext = url.Substring(extStart);
                        }

                        string name = "movie-" + movieEvent.Id.ToString() + "-M" + ext;

                        movieEvent.PosterM_Local = DownloadFile(url, basePath, name, 0);
                    }
                }

                dbMovies.SaveChanges();    
            }
        }

        private void ResizeFile(string imageFile, int width)
        {
            using(FileStream imageStream = new FileStream(imageFile,FileMode.Open, FileAccess.Read))
            using(var image = new Bitmap(imageStream))
            {
                if (image.Width > width * 11 / 10)
                {
                    int height = (int)Math.Round((decimal)(image.Height * width) / image.Width);
                    logger.LogInformation($"Resizing image {imageFile} from {image.Width}x{image.Height} to {width}x{height}");
                    var resized = new Bitmap(width, height);
                    using (var graphics = Graphics.FromImage(resized))
                    {
                        graphics.CompositingQuality = CompositingQuality.HighSpeed;
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        graphics.CompositingMode = CompositingMode.SourceCopy;
                        graphics.DrawImage(image, 0, 0, width, height);
                        resized.Save(imageFile, ImageFormat.Jpeg);
                    }
                }
            }
        }

        private string DownloadFile(string url, string basePath, string name, int resize)
        {
            if (string.IsNullOrEmpty(url))
            {
                return null;
            }

            string target = Path.Combine(basePath, name);

            try
            {
                logger.LogInformation($"Downloading {url} to {target}");
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
            catch (Exception x)
            {
                logger.LogInformation($"FAILED download of {url}, Exception={x.Message}");
                return null;
            }

            if (resize > 0)
            {
                ResizeFile(target, resize);
            }

            return name;
        }

        private void UpdateEpgDataWithImdb()
        {
            UpdateGenericDataWithImdb<MovieEvent>((dbMovies) => dbMovies.MovieEvents);
        }

        private void UpdateGenericDataWithImdb<T>(Func<FxMoviesDbContext, IQueryable<IHasImdbLink>> fnGetMovies) 
        where T : IHasImdbLink
        {
            // aws s3api get-object --request-payer requester --bucket imdb-datasets --key documents/v1/current/title.basics.tsv.gz title.basics.tsv.gz
            // aws s3api get-object --request-payer requester --bucket imdb-datasets --key documents/v1/current/title.ratings.tsv.gz title.ratings.tsv.gz

            int imdbHuntingYearDiff = updateEpgCommandOptions.ImdbHuntingYearDiff ?? 2;

            using (var dbMovies = fxMoviesDbContextFactory.CreateDbContext())
            using (var dbImdb = imdbDbContextFactory.CreateDbContext())
            {
                var huntingProcedure = new List<Func<IHasImdbLink, IQueryable<ImdbDB.Movie>>>();

                // Search for PrimaryTitle (Year)
                huntingProcedure.Add((Func<IHasImdbLink, IQueryable<ImdbDB.Movie>>)
                (
                    (movieWithImdbLink) => {
                        string normalizedTitle = ImdbDB.Util.NormalizeTitle(movieWithImdbLink.Title);
                        return dbImdb.MovieAlternatives
                            .Where(ma =>
                                ma.AlternativeTitle == null 
                                && ma.Normalized == normalizedTitle
                                && (!ma.Movie.Year.HasValue || !movieWithImdbLink.Year.HasValue || ma.Movie.Year == movieWithImdbLink.Year)
                            ).Select(ma => ma.Movie);
                        }
                ));

                // Search for AlternativeTitle (Year)
                huntingProcedure.Add((Func<IHasImdbLink, IQueryable<ImdbDB.Movie>>)
                (
                    (movieWithImdbLink) => {
                        string normalizedTitle = ImdbDB.Util.NormalizeTitle(movieWithImdbLink.Title);
                        return dbImdb.MovieAlternatives
                            .Where(ma => 
                                ma.AlternativeTitle != null 
                                && ma.Normalized == normalizedTitle
                                && (!ma.Movie.Year.HasValue || !movieWithImdbLink.Year.HasValue || ma.Movie.Year == movieWithImdbLink.Year)
                            ).Select(ma => ma.Movie);
                        }
                ));

                // Search for PrimaryTitle (+/-Year)
                huntingProcedure.Add((Func<IHasImdbLink, IQueryable<ImdbDB.Movie>>)
                (
                    (movieWithImdbLink) => {
                        string normalizedTitle = ImdbDB.Util.NormalizeTitle(movieWithImdbLink.Title);
                        return dbImdb.MovieAlternatives
                            .Where(ma =>
                                ma.AlternativeTitle == null 
                                && ma.Normalized == normalizedTitle
                                && (!ma.Movie.Year.HasValue || !movieWithImdbLink.Year.HasValue 
                                    || ((ma.Movie.Year >= movieWithImdbLink.Year - imdbHuntingYearDiff) && (ma.Movie.Year <= movieWithImdbLink.Year + imdbHuntingYearDiff)))
                            ).Select(ma => ma.Movie);
                        }
                ));

                // Search for AlternativeTitle (+/-Year)
                huntingProcedure.Add((Func<IHasImdbLink, IQueryable<ImdbDB.Movie>>)
                (
                    (movieWithImdbLink) => {
                        string normalizedTitle = ImdbDB.Util.NormalizeTitle(movieWithImdbLink.Title);
                        return dbImdb.MovieAlternatives
                            .Where(ma => 
                                ma.AlternativeTitle != null 
                                && ma.Normalized == normalizedTitle
                                && (!ma.Movie.Year.HasValue || !movieWithImdbLink.Year.HasValue 
                                    || ((ma.Movie.Year >= movieWithImdbLink.Year - imdbHuntingYearDiff) && (ma.Movie.Year <= movieWithImdbLink.Year + imdbHuntingYearDiff)))
                            ).Select(ma => ma.Movie);
                        }
                ));

                // Search for AlternativeTitle (+/-Year)
                huntingProcedure.Add((Func<IHasImdbLink, IQueryable<ImdbDB.Movie>>)
                (
                    (movieWithImdbLink) => 
                        dbImdb.MovieAlternatives
                            .Where(m => 
                                m.AlternativeTitle.ToLower() == movieWithImdbLink.Title.ToLower()
                                && (!m.Movie.Year.HasValue || !movieWithImdbLink.Year.HasValue 
                                    || ((m.Movie.Year >= movieWithImdbLink.Year - imdbHuntingYearDiff) && (m.Movie.Year <= movieWithImdbLink.Year + imdbHuntingYearDiff)))
                            ).Select(m => m.Movie)
                ));

                var groups = fnGetMovies(dbMovies).AsEnumerable().GroupBy(m => new { m.Title, m.Year });
                int totalCount = groups.Count();
                int current = 0;
                foreach (var group in groups) //.ToList())
                {
                    current++;
                                        
                    // if (group.Any(m => m.Movie != null) && false)
                    // {
                    //     var firstMovieWithImdbLink = group.First(m => m.Movie != null);
                    //     foreach (var other in group.Where(m => m.Movie == null))
                    //     {
                    //         other.Movie = firstMovieWithImdbLink.Movie;
                    //     }
                    //     continue;
                    // }

                    ImdbDB.Movie imdbMovie = null;
                    var first = group.First();
                    int huntNo = 0;
                    foreach (var hunt in huntingProcedure)
                    {                        
                        imdbMovie = hunt(first)
                            .OrderByDescending(m => m.Votes)
                            .FirstOrDefault();

                        // if (hunt is Func<IHasImdbLink, Movie, bool> huntTyped1)
                        // {
                        //     movie = dbImdb.Movies
                        //         .Where(m => huntTyped1(firstMovieWithImdbLink, m))
                        //         .OrderByDescending(m => m.Votes)
                        //         .FirstOrDefault();
                        // }
                        // else if (hunt is Tuple<Func<IHasImdbLink, MovieAlternative, bool>, Func<IHasImdbLink, Movie, bool>> huntTyped2)
                        // {
                        //     var movieAlternatives = dbImdb.MovieAlternatives.Where(m => huntTyped2.Item1(firstMovieWithImdbLink, m));
                        //     movie = dbImdb.Movies.Join(movieAlternatives, m => m.Id, ma => ma.Id, (m, ma) => m)
                        //         .Where(m => huntTyped2.Item2(firstMovieWithImdbLink, m))
                        //         .OrderByDescending(m => m.Votes)
                        //         .FirstOrDefault();
                        // }
                        // else
                        // {
                        //     throw new InvalidOperationException($"Unknown hunt type {hunt}");
                        // }

                        if (imdbMovie != null)
                            break;
                    
                        huntNo++;
                    }
                    
                    if (imdbMovie == null)
                    {
                        // foreach (var movieWithImdbLink in group)
                        // {
                        //     movieWithImdbLink.Movie.ImdbId = "";
                        // }
                        dbMovies.SaveChanges();
                        logger.LogInformation($"UpdateEpgDataWithImdb: Could not find movie '{first.Title} ({first.Year})' in IMDb");
                        continue;
                    }

                    logger.LogInformation($"{(100 * current) / totalCount}% {first.Title} ({first.Year}) ==> {imdbMovie.ImdbId}, duplicity={group.Count()}, HUNT#{huntNo}");

                    var movie = dbMovies.Movies.SingleOrDefault(m => m.ImdbId == imdbMovie.ImdbId);

                    if (movie == null)
                    {
                        movie = new FxMoviesDB.Movie();
                        movie.ImdbId = imdbMovie.ImdbId;
                    }

                    movie.ImdbRating = imdbMovie.Rating;
                    movie.ImdbVotes = imdbMovie.Votes;
                    if (movie.Certification == null)
                        movie.Certification = theMovieDbService.GetCertification(movie.ImdbId) ?? "";

                    foreach (var movieWithImdbLink in group)
                    {
                        movieWithImdbLink.Movie = movie;
                    }

                    dbMovies.SaveChanges();
                }

                dbMovies.SaveChanges();
            }
        }

    }
}