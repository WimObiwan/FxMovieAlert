using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FxMovies.Core.Entities;
using FxMovies.Core.Queries;
using FxMovies.Core.Services;
using FxMovies.FxMoviesDB;
using FxMovies.ImdbDB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace FxMovies.Core.Commands;

public interface IUpdateEpgCommand
{
    Task<int> Execute();
}

public class UpdateEpgCommandOptions
{
    public static string Position => "UpdateEpg";

    public string[] MovieTitlesToIgnore { get; set; }
    public string[] MovieTitlesToTransform { get; set; }
    public string[] YearSplitterPatterns { get; set; }
    public int? MaxDays { get; set; }
    public string ImageBasePath { get; set; }
    public Dictionary<string, string> ImageOverrideMap { get; set; }
    public string[] ActivateProviders { get; set; }
    public enum DownloadImagesOption
    {
        Active,
        Disabled,
        IfNotPresent
    }
    public DownloadImagesOption? DownloadImages { get; set; }
}

public class UpdateEpgCommand : IUpdateEpgCommand
{
    private readonly ILogger<UpdateEpgCommand> logger;
    private readonly FxMoviesDbContext fxMoviesDbContext;
    private readonly ImdbDbContext imdbDbContext;
    private readonly UpdateEpgCommandOptions updateEpgCommandOptions;
    private readonly ITheMovieDbService theMovieDbService;
    private readonly IEnumerable<IMovieEventService> movieEventServices;
    private readonly IHumoService humoService;
    private readonly IImdbMatchingQuery imdbMatchingQuery;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly IManualMatchesQuery manualMatchesQuery;
    private readonly UpdateEpgCommandOptions.DownloadImagesOption downloadImagesOption;

    public IManualMatchesQuery ManualMatchesQuery => manualMatchesQuery;

    public UpdateEpgCommand(ILogger<UpdateEpgCommand> logger, 
        FxMoviesDbContext fxMoviesDbContext, ImdbDbContext imdbDbContext,
        IOptionsSnapshot<UpdateEpgCommandOptions> updateEpgCommandOptions,
        ITheMovieDbService theMovieDbService,
        IEnumerable<IMovieEventService> movieEventServices,
        IHumoService humoService,
        IImdbMatchingQuery imdbMatchingQuery,
        IHttpClientFactory httpClientFactory,
        IManualMatchesQuery manualMatchesQuery)
    {
        this.logger = logger;
        this.fxMoviesDbContext = fxMoviesDbContext;
        this.imdbDbContext = imdbDbContext;
        this.updateEpgCommandOptions = updateEpgCommandOptions.Value;
        this.theMovieDbService = theMovieDbService;
        this.movieEventServices = movieEventServices;
        this.humoService = humoService;
        this.imdbMatchingQuery = imdbMatchingQuery;
        this.httpClientFactory = httpClientFactory;
        this.manualMatchesQuery = manualMatchesQuery;

        this.downloadImagesOption = this.updateEpgCommandOptions.DownloadImages ?? UpdateEpgCommandOptions.DownloadImagesOption.Active;
    }

    public async Task<int> Execute()
    {
        Exception firstException = null;
        int failedOperations = 0;

        try
        {
            await UpdateDatabaseEpg();
        }
        catch (Exception x)
        {
            logger.LogError(x, "UpdateDatabaseEpg failed.  Trying to continue.");
            failedOperations++;
            if (firstException == null)
                firstException = x;
        }

        try
        {
            await UpdateEpgDataWithImdb();
        }
        catch (Exception x)
        {
            logger.LogError(x, "UpdateEpgDataWithImdb failed.  Trying to continue.");
            failedOperations++;
            if (firstException == null)
                firstException = x;
        }

        try
        {
            await UpdateMissingImageLinks();
        }
        catch (Exception x)
        {
            logger.LogError(x, "UpdateMissingImageLinks failed.  Trying to continue.");
            failedOperations++;
            if (firstException == null)
                firstException = x;
        }

        if (downloadImagesOption != UpdateEpgCommandOptions.DownloadImagesOption.Disabled)
        {
            try
            {
                await DownloadImageData();
            }
            catch (Exception x)
            {
                logger.LogError(x, "DownloadImageData failed.  Trying to continue.");
                failedOperations++;
                if (firstException == null)
                    firstException = x;
            }
        }

        if (firstException != null)
        {
            throw new Exception($"UpdateEpgCommand.Run failed for {failedOperations} operations.  InnerException contains first failure.", 
                firstException);
        }

        return 0;
    }

    private bool IsProviderActivated(string provider)
    {
        var activateProviders = updateEpgCommandOptions.ActivateProviders;
        return activateProviders == null
            || !updateEpgCommandOptions.ActivateProviders.Any()
            || updateEpgCommandOptions.ActivateProviders.Contains(provider);
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

        Exception firstException = null;
        int failedProviders = 0;

        foreach (var service in movieEventServices)
        {
            var channelCode = service.ChannelCode;
            if (IsProviderActivated(channelCode))
            {
                try
                {
                    var movieEvents = await service.GetMovieEvents();
                    await UpdateMovieEvents(movieEvents, (MovieEvent me) => me.Vod && me.Channel.Code == channelCode);
                }
                catch (Exception x)
                {
                    logger.LogError(x, "UpdateDatabaseEpg failed for {Provider}.  Trying to continue with other providers.", service.ProviderName);
                    failedProviders++;
                    if (firstException == null)
                        firstException = x;
                }
            }
            else
            {
                logger.LogWarning("UpdateDatabaseEpg disabled for {Provider}.", service.ProviderName);
            }
        }

        if (IsProviderActivated("humo"))
        {
            try
            {
                await UpdateDatabaseEpg_Humo();
            }
            catch (Exception x)
            {
                logger.LogError(x, "UpdateDatabaseEpg failed for Humo.  Trying to continue with other providers.");
                failedProviders++;
                if (firstException == null)
                    firstException = x;
            }
        }

        if (firstException != null)
        {
            throw new Exception($"UpdateDatabaseEpg failed for {failedProviders} providers.  InnerException contains first failure.", 
                firstException);
        }
    }

    private async Task UpdateDatabaseEpg_Humo()
    {
        DateTime now = DateTime.Now;

        int maxDays = updateEpgCommandOptions.MaxDays ?? 7;
        for (int days = 0; days <= maxDays; days++)
        {
            DateTime date = now.Date.AddDays(days);
            var movieEvents = await humoService.GetGuide(date);

            await UpdateMovieEvents(movieEvents, (MovieEvent me) => !me.Vod && me.StartTime.Date == date);
        }
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
            logger.LogInformation("Ignoring movie: {Id} {Title}", movie.Id, movie.Title);
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
                    logger.LogInformation("Transforming movie: {Id} {Title} to {NewTitle}", movie.Id, movie.Title, newTitle);
                    movie.Title = newTitle;
                }
            }
        }

        foreach (var movie in movieEvents)
        {
            logger.LogInformation("{ChannelName} {Id} {Title} {Year} {StartTime}",
                movie.Channel.Name, movie.Id, movie.Title, movie.Year, movie.StartTime);
        }

        var existingMovies = fxMoviesDbContext.MovieEvents.Where(movieEventsSubset);
        logger.LogInformation("Existing movies: {ExistingMovieCount}", await existingMovies.CountAsync());
        logger.LogInformation("New movies: {NewMovieCount}", movieEvents.Count());

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
            logger.LogInformation("Existing movies to be removed: {ExistingMoviesToRemove}", remove.Count());
            fxMoviesDbContext.RemoveRange(remove);
            await fxMoviesDbContext.SaveChangesAsync();
        }

        foreach (var movie in movieEvents)
        {
            var existingDuplicates = await existingMovies
                .Where(me => me.ExternalId == movie.ExternalId)
                .OrderBy(me => me.Id)
                .Skip(1)
                .ToListAsync();
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
                if (existingMovie.AddedTime == null)
                    existingMovie.AddedTime = DateTime.UtcNow;
            }
            else
            {
                movie.AddedTime = DateTime.UtcNow;
                fxMoviesDbContext.MovieEvents.Add(movie);
            }
        }

        await fxMoviesDbContext.SaveChangesAsync();
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
                    logger.LogError(x, "UpdateMissingImageLinks failed for movie {ImdbId}", movieEvent.Movie.ImdbId);
                }
            }
        }
        await fxMoviesDbContext.SaveChangesAsync();
    }

    private async Task DownloadImageData()
    {
        if (downloadImagesOption == UpdateEpgCommandOptions.DownloadImagesOption.Disabled)
            return;

        string basePath = updateEpgCommandOptions.ImageBasePath;
        if (!Directory.Exists(basePath))
            Directory.CreateDirectory(basePath);

        foreach (var channel in fxMoviesDbContext.Channels)
        {
            string url = channel.LogoS;

            if (url == null)
                continue;

            if (CheckDownloadNotNeeded(url, channel.LogoS_Local))
            {
                logger.LogInformation("Skipping existing image {url}, local {local}",
                    url, channel.LogoS_Local);
                continue;
            }

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

                if (CheckDownloadNotNeeded(url, movieEvent.PosterS_Local))
                {
                    logger.LogInformation("Skipping existing image {url}, local {local}",
                        url, movieEvent.PosterS_Local);
                    continue;
                }

                string name = "movie-" + movieEvent.Id.ToString() + "-S";

                movieEvent.PosterS_Local = await DownloadFile(url, basePath, name, 150);
            }

            if (movieEvent.PosterM == movieEvent.PosterS)
            {
                movieEvent.PosterM_Local = movieEvent.PosterS_Local;
            }
            else
            {
                string url = movieEvent.PosterM;

                if (url == null)
                    continue;

                if (CheckDownloadNotNeeded(url, movieEvent.PosterM_Local))
                {
                    logger.LogInformation("Skipping existing image {url}, local {local}",
                        url, movieEvent.PosterM_Local);
                    continue;
                }

                string name = "movie-" + movieEvent.Id.ToString() + "-M";

                movieEvent.PosterM_Local = await DownloadFile(url, basePath, name, 0);
            }
        }

        await fxMoviesDbContext.SaveChangesAsync();    
    }

    private bool CheckDownloadNotNeeded(string url, string localFile)
    {
        if (downloadImagesOption == UpdateEpgCommandOptions.DownloadImagesOption.Active)
            return false;
        if (downloadImagesOption == UpdateEpgCommandOptions.DownloadImagesOption.Disabled)
            return true;
        if (downloadImagesOption == UpdateEpgCommandOptions.DownloadImagesOption.IfNotPresent)
        {
            if (string.IsNullOrEmpty(localFile))
                return false;
            
            string file = Path.Combine(updateEpgCommandOptions.ImageBasePath, localFile);
            return File.Exists(file);
        }

        throw new InvalidOperationException();
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
            logger.LogWarning(x, "Failed to resize {ImageFile}", imageFile);
        }
    }

    private async Task<string> DownloadFile(string url, string basePath, string name, int resize)
    {
        if (string.IsNullOrEmpty(url))
            return null;

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
            logger.LogInformation("Downloading {Url} to {FileName}, {ContentType}",
                url, target, contentType);
            using (var stm = await rsp.Content.ReadAsStreamAsync())
            using (var fileStream = File.Create(target))
            {
                await stm.CopyToAsync(fileStream);
            }
        }
        catch (Exception x)
        {
            logger.LogWarning(x, "FAILED download of {Url}", url);
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

            var first = group.First();
            var imdbMatchingQueryResult = await imdbMatchingQuery.Execute(first.Title, first.Year);
            var imdbMovie = imdbMatchingQueryResult.ImdbMovie;
            var huntNo = imdbMatchingQueryResult.HuntNo;

            Movie movie = null;
            if (imdbMovie == null)
            {
                huntNo = -1;

                var manualMatch = await ManualMatchesQuery.Execute(first.Title);
                movie = manualMatch?.Movie;
                if (movie != null)
                {
                    logger.LogInformation("UpdateEpgDataWithImdb: Fallback using ManualMatch for '{Title} ({Year})' to existing Movie {MovieID} {ImdbId}",
                        first.Title, first.Year, movie.Id, movie.ImdbId);

                    imdbMovie = await imdbDbContext.Movies.FirstOrDefaultAsync(m => m.ImdbId == movie.ImdbId);
                }
            }

            if (movie == null && imdbMovie == null)
            {
                logger.LogInformation("UpdateEpgDataWithImdb: Could not find movie '{Title} ({Year})' in IMDb",
                    first.Title, first.Year);
                continue;
            }

            logger.LogInformation("{PercentDone}% {Title} ({Year}) ==> {ImdbId}, duplicity={Duplicity}, HUNT#{HuntNo}",
                (100 * current) / totalCount, first.Title, first.Year,
                movie?.ImdbId ?? imdbMovie?.ImdbId, group.Count(), huntNo);

            if (movie == null)
                movie = await fxMoviesDbContext.Movies.SingleOrDefaultAsync(m => m.ImdbId == imdbMovie.ImdbId);

            if (movie == null)
            {
                movie = new Movie();
                movie.ImdbId = imdbMovie?.ImdbId;
            }

            if (imdbMovie != null)
            {
                movie.ImdbRating = imdbMovie.Rating;
                movie.ImdbVotes = imdbMovie.Votes;
            }

            if (movie.Certification == null)
                movie.Certification = (await theMovieDbService.GetCertification(movie.ImdbId)) ?? "";

            foreach (var movieWithImdbLink in group)
            {
                movieWithImdbLink.Movie = movie;
            }
        }

        await fxMoviesDbContext.SaveChangesAsync();
    }
}
