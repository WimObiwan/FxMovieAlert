using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FxMovies.FxMoviesDB;
using FxMovies.ImdbDB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace FxMovies.Core
{
    public interface IUpdateEpgCommand
    {
        Task<int> Run();
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
        private readonly FxMoviesDbContext fxMoviesDbContext;
        private readonly ImdbDbContext imdbDbContext;
        private readonly UpdateEpgCommandOptions updateEpgCommandOptions;
        private readonly ITheMovieDbService theMovieDbService;
        private readonly IVtmGoService vtmGoService;
        private readonly IVrtNuService vrtNuService;
        private readonly IHumoService humoService;
        private readonly IHttpClientFactory httpClientFactory;

        public UpdateEpgCommand(ILogger<UpdateEpgCommand> logger, 
            FxMoviesDbContext fxMoviesDbContext, ImdbDbContext imdbDbContext,
            IOptionsSnapshot<UpdateEpgCommandOptions> updateEpgCommandOptions,
            ITheMovieDbService theMovieDbService,
            IVtmGoService vtmGoService, 
            IVrtNuService vrtNuService, 
            IHumoService humoService,
            IHttpClientFactory httpClientFactory)
        {
            this.logger = logger;
            this.fxMoviesDbContext = fxMoviesDbContext;
            this.imdbDbContext = imdbDbContext;
            this.updateEpgCommandOptions = updateEpgCommandOptions.Value;
            this.theMovieDbService = theMovieDbService;
            this.vtmGoService = vtmGoService;
            this.vrtNuService = vrtNuService;
            this.humoService = humoService;
            this.httpClientFactory = httpClientFactory;
        }

        public async Task<int> Run()
        {
            await UpdateDatabaseEpg();
            await UpdateEpgDataWithImdb();
            await UpdateMissingImageLinks();
            await DownloadImageData();
            //UpdateDatabaseEpgHistory();
            return 0;
        }

        private async Task UpdateDatabaseEpg()
        {
            DateTime now = DateTime.Now;

            // Remove all old MovieEvents
            var set = fxMoviesDbContext.MovieEvents.Where(x => 
                x.Vod == false && x.StartTime < now.Date 
                || x.Vod == true && x.EndTime.HasValue && x.EndTime.Value < now.Date);
            fxMoviesDbContext.MovieEvents.RemoveRange(set);
            await fxMoviesDbContext.SaveChangesAsync();

            await UpdateDatabaseEpg_VtmGo();
            await UpdateDatabaseEpg_VrtNu();
            await UpdateDatabaseEpg_Humo();
        }

        private async Task UpdateDatabaseEpg_VtmGo()
        {
            var movieEvents = await vtmGoService.GetMovieEvents();
            await UpdateMovieEvents(movieEvents, (MovieEvent me) => me.Channel.Code == "vtmgo");
        }

        private async Task UpdateDatabaseEpg_VrtNu()
        {
            var movieEvents = await vrtNuService.GetMovieEvents();
            await UpdateMovieEvents(movieEvents, (MovieEvent me) => me.Channel.Code == "vrtnu");
        }

        private async Task UpdateMovieEvents(IList<MovieEvent> movieEvents, Expression<Func<MovieEvent, bool>> movieEventsSubset)
        {
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
            foreach (var movie in movieEvents.Where(isMovieIgnored))
            {
                logger.LogInformation($"Ignoring movie: {movie.Id} {movie.Title}");
            }
            movieEvents = movieEvents.Where(m => !isMovieIgnored(m)).ToList();

            // Transform movie titles
            foreach (var movie in movieEvents)
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

            foreach (var movie in movieEvents)
            {
                logger.LogInformation($"{movie.Channel.Name} {movie.Title} {movie.Year} {movie.StartTime}");
            }

            var existingMovies = fxMoviesDbContext.MovieEvents.Where(movieEventsSubset);
            logger.LogInformation("Existing movies: {0}", await existingMovies.CountAsync());
            logger.LogInformation("New movies: {0}", movieEvents.Count());

            // Update channels
            foreach (var channel in movieEvents.Select(m => m.Channel).Distinct())
            {
                Channel existingChannel = await fxMoviesDbContext.Channels.SingleOrDefaultAsync(c => c.Code == channel.Code);
                if (existingChannel != null)
                {
                    existingChannel.Name = channel.Name;
                    existingChannel.LogoS = channel.LogoS;
                    fxMoviesDbContext.Channels.Update(existingChannel);
                    foreach (var movie in movieEvents.Where(m => m.Channel == channel))
                        movie.Channel = existingChannel;
                }
                else
                {
                    fxMoviesDbContext.Channels.Add(channel);
                }
            }

            // Remove exising movies that don't appear in new movies
            {
                var remove = existingMovies.ToList().Where(m1 => !movieEvents.Any(m2 => m2.ExternalId == m1.ExternalId));
                logger.LogInformation("Existing movies to be removed: {0}", remove.Count());
                fxMoviesDbContext.RemoveRange(remove);
                await fxMoviesDbContext.SaveChangesAsync();
            }

            foreach (var movie in movieEvents)
            {
                var existingDuplicates = await existingMovies.Where(me => me.ExternalId == movie.ExternalId).Skip(1).ToListAsync();
                fxMoviesDbContext.MovieEvents.RemoveRange(existingDuplicates);
                await fxMoviesDbContext.SaveChangesAsync();
            }

            // Update movies
            foreach (var movie in movieEvents)
            {
                var existingMovie = await existingMovies.Include(me => me.Movie).SingleOrDefaultAsync(me => me.ExternalId == movie.ExternalId);
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
                    existingMovie.Vod = movie.Vod;
                    existingMovie.VodLink = movie.VodLink;
                }
                else
                {
                    fxMoviesDbContext.MovieEvents.Add(movie);
                }
            }

            // {
            //     set.RemoveRange(set.Where(x => x.StartTime.Date == date));
            //     db.SaveChanges();
            // }

            await fxMoviesDbContext.SaveChangesAsync();

            // using (var db = fxMoviesDbContextFactory.CreateDbContext())
            // {
            //     // Remove all old MovieEvents
            //     {
            //         var set = db.Channels.Where(ch => db.MovieEvents.All(me => me.Channel != ch));
            //         db.RemoveRange(set);
            //     }
            //     await db.SaveChangesAsync();
            // }
        }

        private async Task UpdateDatabaseEpg_Humo()
        {
            DateTime now = DateTime.Now;

            int maxDays = updateEpgCommandOptions.MaxDays ?? 7;
            for (int days = 0; days <= maxDays; days++)
            {
                DateTime date = now.Date.AddDays(days);
                var movies = await humoService.GetGuide(date);

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

                var existingMovies = fxMoviesDbContext.MovieEvents.Where(x => x.StartTime.Date == date);
                logger.LogInformation("Existing movies: {0}", await existingMovies.CountAsync());
                logger.LogInformation("New movies: {0}", movies.Count());

                // Update channels
                foreach (var channel in movies.Select(m => m.Channel).Distinct())
                {
                    Channel existingChannel = await fxMoviesDbContext.Channels.SingleOrDefaultAsync(c => c.Code == channel.Code);
                    if (existingChannel != null)
                    {
                        existingChannel.Name = channel.Name;
                        existingChannel.LogoS = channel.LogoS;
                        fxMoviesDbContext.Channels.Update(existingChannel);
                        foreach (var movie in movies.Where(m => m.Channel == channel))
                            movie.Channel = existingChannel;
                    }
                    else
                    {
                        fxMoviesDbContext.Channels.Add(channel);
                    }
                }

                // Remove exising movies that don't appear in new movies
                {
                    var remove = existingMovies.ToList().Where(m1 => !movies.Any(m2 => m2.Id == m1.Id));
                    logger.LogInformation("Existing movies to be removed: {0}", remove.Count());
                    fxMoviesDbContext.RemoveRange(remove);
                }

                // Update movies
                foreach (var movie in movies)
                {
                    var existingMovie = fxMoviesDbContext.MovieEvents.Find(movie.Id);
                    if (existingMovie != null)
                    {
                        if (existingMovie.Title != movie.Title)
                        {
                            existingMovie.Title = movie.Title;
                            existingMovie.Movie = null;
                        }
                        existingMovie.Year = movie.Year;
                        if (movie.StartTime != DateTime.MinValue)
                            existingMovie.StartTime = movie.StartTime;
                        else if (existingMovie.StartTime == DateTime.MinValue)
                            existingMovie.StartTime = DateTime.Today;
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
                        if (movie.StartTime == DateTime.MinValue)
                            movie.StartTime = DateTime.Today;
                        fxMoviesDbContext.MovieEvents.Add(movie);
                    }
                }

                // {
                //     set.RemoveRange(set.Where(x => x.StartTime.Date == date));
                //     db.SaveChanges();
                // }

                await fxMoviesDbContext.SaveChangesAsync();
            }

            // using (var db = fxMoviesDbContextFactory.CreateDbContext())
            // {
            //     // Remove all old MovieEvents
            //     {
            //         var set = db.Channels.Where(ch => db.MovieEvents.All(me => me.Channel != ch));
            //         db.RemoveRange(set);
            //     }
            //     await db.SaveChangesAsync();
            // }
        }

        private async Task UpdateMissingImageLinks()
        {
            foreach (var movieEvent in fxMoviesDbContext.MovieEvents)
            {
                bool emptyPosterM = string.IsNullOrEmpty(movieEvent.PosterM);
                bool emptyPosterS = string.IsNullOrEmpty(movieEvent.PosterS);
                if ((emptyPosterM || emptyPosterS) && !string.IsNullOrEmpty(movieEvent.Movie?.ImdbId))
                {
                    try
                    {
                        var result = await theMovieDbService.GetImages(movieEvent.Movie.ImdbId);
                        movieEvent.PosterM = result.Medium;
                        movieEvent.PosterS = result.Small;
                    }
                    catch(Exception x)
                    {
                        logger.LogError(x, $"UpdateMissingImageLinks failed for movie {movieEvent.Movie.ImdbId}");
                    }
                }
            }
            await fxMoviesDbContext.SaveChangesAsync();
        }

        private async Task DownloadImageData()
        {
            string basePath = updateEpgCommandOptions.ImageBasePath;
            if (!Directory.Exists(basePath))
                Directory.CreateDirectory(basePath);

            foreach (var channel in fxMoviesDbContext.Channels)
            {
                string url = channel.LogoS;

                if (url == null)
                    continue;

                string name = "channel-" + channel.Code;

                if (updateEpgCommandOptions.ImageOverrideMap.TryGetValue(name, out string imageOverride))
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
                    channel.LogoS_Local = await DownloadFile(url, basePath, name, 50);

                    // channel.LogoS_ETag = eTag;
                    // channel.LogoS_LastModified = lastModified;
                }
            }

            foreach (var movieEvent in fxMoviesDbContext.MovieEvents)
            {
                {
                    string url = movieEvent.PosterS;

                    if (url == null)
                        continue;

                    string name = "movie-" + movieEvent.Id.ToString() + "-S";

                    movieEvent.PosterS_Local = await DownloadFile(url, basePath, name, 150);
                }
                {
                    string url = movieEvent.PosterM;

                    if (url == null)
                        continue;

                    string name = "movie-" + movieEvent.Id.ToString() + "-M";

                    movieEvent.PosterM_Local = await DownloadFile(url, basePath, name, 0);
                }
            }

            await fxMoviesDbContext.SaveChangesAsync();    
        }

        private async Task ResizeFile(string imageFile, int width)
        {
            try
            {
                using (Image image = await Image.LoadAsync(imageFile))
                {
                    image.Mutate(i => i.Resize(width, 0));
                    image.Save(imageFile);
                }
            }
            catch (Exception x)
            {
                logger.LogWarning(x, $"Failed to resize {imageFile}");
            }
        }

        private async Task<string> DownloadFile(string url, string basePath, string name, int resize)
        {
            if (string.IsNullOrEmpty(url))
            {
                return null;
            }

            string target;

            try
            {
                var client = httpClientFactory.CreateClient("images");
                var rsp = await client.GetAsync(url);
                string ext;
                string contentType = rsp.Content.Headers.ContentType.MediaType;
                if (contentType.Equals("image/png", StringComparison.InvariantCultureIgnoreCase)) {
                    ext = ".png";
                } else {
                    ext = ".jpg";
                }
                name = name + ext;
                target = Path.Combine(basePath, name);
                logger.LogInformation($"Downloading {url} to {target}, {contentType}");
                using (var stm = await rsp.Content.ReadAsStreamAsync())
                using (var fileStream = File.Create(target))
                {
                    await stm.CopyToAsync(fileStream);
                }
            }
            catch (Exception x)
            {
                logger.LogWarning($"FAILED download of {url}, Exception={x.Message}");
                return null;
            }

            if (resize > 0)
            {
                await ResizeFile(target, resize);
            }

            return name;
        }

        private async Task UpdateEpgDataWithImdb()
        {
            await UpdateGenericDataWithImdb<MovieEvent>((dbMovies) => dbMovies.MovieEvents.Include(me => me.Movie));
        }

        private async Task UpdateGenericDataWithImdb<T>(Func<FxMoviesDbContext, IQueryable<IHasImdbLink>> fnGetMovies) 
        where T : IHasImdbLink
        {
            int imdbHuntingYearDiff = updateEpgCommandOptions.ImdbHuntingYearDiff ?? 2;

            var huntingProcedure = new List<Func<IHasImdbLink, IQueryable<ImdbDB.Movie>>>();

            // Search for PrimaryTitle (Year)
            huntingProcedure.Add((Func<IHasImdbLink, IQueryable<ImdbDB.Movie>>)
            (
                (movieWithImdbLink) => {
                    string normalizedTitle = ImdbDB.Util.NormalizeTitle(movieWithImdbLink.Title);
                    return imdbDbContext.MovieAlternatives
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
                    return imdbDbContext.MovieAlternatives
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
                    return imdbDbContext.MovieAlternatives
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
                    return imdbDbContext.MovieAlternatives
                        .Where(ma => 
                            ma.AlternativeTitle != null 
                            && ma.Normalized == normalizedTitle
                            && (!ma.Movie.Year.HasValue || !movieWithImdbLink.Year.HasValue 
                                || ((ma.Movie.Year >= movieWithImdbLink.Year - imdbHuntingYearDiff) && (ma.Movie.Year <= movieWithImdbLink.Year + imdbHuntingYearDiff)))
                        ).Select(ma => ma.Movie);
                    }
            ));

            var groups = fnGetMovies(fxMoviesDbContext).AsEnumerable().GroupBy(m => new { m.Title, m.Year });
            int totalCount = groups.Count();
            int current = 0;
            foreach (var group in groups) //.ToList())
            {
                current++;
                                    
                if (group.Any(m => m.Movie != null))
                {
                    var firstMovieWithImdbLink = group.First(m => m.Movie != null);
                    foreach (var other in group.Where(m => m.Movie == null))
                    {
                        other.Movie = firstMovieWithImdbLink.Movie;
                    }

                    await fxMoviesDbContext.SaveChangesAsync();
                    continue;
                }

                ImdbDB.Movie imdbMovie = null;
                var first = group.First();
                int huntNo = 0;
                foreach (var hunt in huntingProcedure)
                {                        
                    imdbMovie = await hunt(first)
                        .OrderByDescending(m => m.Votes)
                        .FirstOrDefaultAsync();

                    if (imdbMovie != null)
                        break;
                
                    huntNo++;
                }
                
                if (imdbMovie == null)
                {
                    await fxMoviesDbContext.SaveChangesAsync();
                    logger.LogInformation($"UpdateEpgDataWithImdb: Could not find movie '{first.Title} ({first.Year})' in IMDb");
                    continue;
                }

                logger.LogInformation($"{(100 * current) / totalCount}% {first.Title} ({first.Year}) ==> {imdbMovie.ImdbId}, duplicity={group.Count()}, HUNT#{huntNo}");

                var movie = await fxMoviesDbContext.Movies.SingleOrDefaultAsync(m => m.ImdbId == imdbMovie.ImdbId);

                if (movie == null)
                {
                    movie = new FxMoviesDB.Movie();
                    movie.ImdbId = imdbMovie.ImdbId;
                }

                movie.ImdbRating = imdbMovie.Rating;
                movie.ImdbVotes = imdbMovie.Votes;
                if (movie.Certification == null)
                    movie.Certification = (await theMovieDbService.GetCertification(movie.ImdbId)) ?? "";

                foreach (var movieWithImdbLink in group)
                {
                    movieWithImdbLink.Movie = movie;
                }

                await fxMoviesDbContext.SaveChangesAsync();
            }

            await fxMoviesDbContext.SaveChangesAsync();
        }

    }
}