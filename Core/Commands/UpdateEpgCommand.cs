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
using FxMovies.ImdbDB;
using FxMovies.MoviesDB;
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
    public enum DownloadImagesOption
    {
        Active,
        Disabled,
        IfNotPresent
    }

    public static string Position => "UpdateEpg";

    public string[] MovieTitlesToIgnore { get; set; }
    public string[] MovieTitlesToTransform { get; set; }
    public string[] YearSplitterPatterns { get; set; }
    public int? MaxDays { get; set; }
    public string ImageBasePath { get; set; }

    // Resharper disable All
    public Dictionary<string, string> ImageOverrideMap { get; set; }
    // Resharper restore All

    public string[] ActivateProviders { get; set; }

    public DownloadImagesOption? DownloadImages { get; set; }
}

public class UpdateEpgCommand : IUpdateEpgCommand
{
    private readonly UpdateEpgCommandOptions.DownloadImagesOption _downloadImagesOption;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHumoService _humoService;
    private readonly ImdbDbContext _imdbDbContext;
    private readonly IImdbMatchingQuery _imdbMatchingQuery;
    private readonly ILogger<UpdateEpgCommand> _logger;
    private readonly IManualMatchesQuery _manualMatchesQuery;
    private readonly IEnumerable<IMovieEventService> _movieEventServices;
    private readonly MoviesDbContext _moviesDbContext;
    private readonly ITheMovieDbService _theMovieDbService;
    private readonly UpdateEpgCommandOptions _updateEpgCommandOptions;

    public UpdateEpgCommand(ILogger<UpdateEpgCommand> logger,
        MoviesDbContext moviesDbContext, ImdbDbContext imdbDbContext,
        IOptionsSnapshot<UpdateEpgCommandOptions> updateEpgCommandOptions,
        ITheMovieDbService theMovieDbService,
        IEnumerable<IMovieEventService> movieEventServices,
        IHumoService humoService,
        IImdbMatchingQuery imdbMatchingQuery,
        IHttpClientFactory httpClientFactory,
        IManualMatchesQuery manualMatchesQuery)
    {
        _logger = logger;
        _moviesDbContext = moviesDbContext;
        _imdbDbContext = imdbDbContext;
        _updateEpgCommandOptions = updateEpgCommandOptions.Value;
        _theMovieDbService = theMovieDbService;
        _movieEventServices = movieEventServices;
        _humoService = humoService;
        _imdbMatchingQuery = imdbMatchingQuery;
        _httpClientFactory = httpClientFactory;
        _manualMatchesQuery = manualMatchesQuery;

        _downloadImagesOption = _updateEpgCommandOptions.DownloadImages ??
                                UpdateEpgCommandOptions.DownloadImagesOption.Active;
    }

    public async Task<int> Execute()
    {
        Exception firstException = null;
        var failedOperations = 0;

        try
        {
            await UpdateDatabaseEpg();
        }
        catch (Exception x)
        {
            _logger.LogError(x, "UpdateDatabaseEpg failed.  Trying to continue.");
            failedOperations++;
            firstException = x;
        }

        try
        {
            await UpdateEpgDataWithImdb();
        }
        catch (Exception x)
        {
            _logger.LogError(x, "UpdateEpgDataWithImdb failed.  Trying to continue.");
            failedOperations++;
            firstException ??= x;
        }

        try
        {
            await UpdateMissingImageLinks();
        }
        catch (Exception x)
        {
            _logger.LogError(x, "UpdateMissingImageLinks failed.  Trying to continue.");
            failedOperations++;
            firstException ??= x;
        }

        if (_downloadImagesOption != UpdateEpgCommandOptions.DownloadImagesOption.Disabled)
            try
            {
                await DownloadImageData();
            }
            catch (Exception x)
            {
                _logger.LogError(x, "DownloadImageData failed.  Trying to continue.");
                failedOperations++;
                firstException ??= x;
            }

        if (firstException != null)
            throw new Exception(
                $"UpdateEpgCommand.Run failed for {failedOperations} operations.  InnerException contains first failure.",
                firstException);

        return 0;
    }

    private bool IsProviderActivated(string provider)
    {
        var activateProviders = _updateEpgCommandOptions.ActivateProviders;
        return activateProviders == null
               || !_updateEpgCommandOptions.ActivateProviders.Any()
               || _updateEpgCommandOptions.ActivateProviders.Contains(provider);
    }

    private async Task UpdateDatabaseEpg()
    {
        var now = DateTime.Now;

        // Remove all old MovieEvents
        var set = _moviesDbContext.MovieEvents.Where(x =>
            x.Vod == false && x.StartTime < now.Date
            || x.Vod == true && x.EndTime.HasValue && x.EndTime.Value < now.Date);
        _moviesDbContext.MovieEvents.RemoveRange(set);
        await _moviesDbContext.SaveChangesAsync();

        Exception firstException = null;
        var failedProviders = 0;

        foreach (var service in _movieEventServices)
        {
            var channelCode = service.ChannelCode;
            if (IsProviderActivated(channelCode))
                try
                {
                    var movieEvents = await service.GetMovieEvents();
                    await UpdateMovieEvents(movieEvents, me => me.Vod && me.Channel.Code == channelCode);
                }
                catch (Exception x)
                {
                    _logger.LogError(x,
                        "UpdateDatabaseEpg failed for {Provider}.  Trying to continue with other providers.",
                        service.ProviderName);
                    failedProviders++;
                    firstException ??= x;
                }
            else
                _logger.LogWarning("UpdateDatabaseEpg disabled for {Provider}.", service.ProviderName);
        }

        if (IsProviderActivated("humo"))
            try
            {
                await UpdateDatabaseEpg_Humo();
            }
            catch (Exception x)
            {
                _logger.LogError(x, "UpdateDatabaseEpg failed for Humo.  Trying to continue with other providers.");
                failedProviders++;
                firstException ??= x;
            }

        if (firstException != null)
            throw new Exception(
                $"UpdateDatabaseEpg failed for {failedProviders} providers.  InnerException contains first failure.",
                firstException);
    }

    private async Task UpdateDatabaseEpg_Humo()
    {
        var now = DateTime.Now;

        var maxDays = _updateEpgCommandOptions.MaxDays ?? 7;
        for (var days = 0; days <= maxDays; days++)
        {
            var date = now.Date.AddDays(days);
            var movieEvents = await _humoService.GetGuide(date);

            await UpdateMovieEvents(movieEvents, me => !me.Vod && me.StartTime.Date == date);
        }
    }

    private async Task UpdateMovieEvents(IList<MovieEvent> movieEvents,
        Expression<Func<MovieEvent, bool>> movieEventsSubset)
    {
        // Remove movies that should be ignored
        bool IsMovieIgnored(MovieEvent movieEvent)
        {
            foreach (var item in _updateEpgCommandOptions.MovieTitlesToIgnore)
                if (Regex.IsMatch(movieEvent.Title, item))
                    return true;
            return false;
        }

        foreach (var movie in movieEvents.Where(IsMovieIgnored))
            _logger.LogInformation("Ignoring movie: {Id} {Title}", movie.Id, movie.Title);
        movieEvents = movieEvents.Where(m => !IsMovieIgnored(m)).ToList();

        // Transform movie titles
        foreach (var movie in movieEvents)
        {
            foreach (var item in _updateEpgCommandOptions.MovieTitlesToTransform)
            {
                var newTitle = Regex.Replace(movie.Title, item, "$1");
                if (movie.Title != newTitle)
                {
                    _logger.LogInformation("Transforming movie: {Id} {Title} to {NewTitle}", movie.Id, movie.Title,
                        newTitle);
                    movie.Title = newTitle;
                }
            }

            foreach (var item in _updateEpgCommandOptions.YearSplitterPatterns)
            {
                var match = Regex.Match(movie.Title, item);
                if (match.Success)
                {
                    var year = match.Groups[2].Value;
                    if (int.TryParse(year, out var yearInt))
                    {
                        movie.Year = yearInt;
                        movie.Title = match.Groups[1].Value.Trim();
                    }
                }
            }

            _logger.LogInformation("{ChannelName} {Id} {Title} {Year} {StartTime}",
                movie.Channel.Name, movie.Id, movie.Title, movie.Year, movie.StartTime);
        }

        var existingMovies = _moviesDbContext.MovieEvents.Where(movieEventsSubset);
        _logger.LogInformation("Existing movies: {ExistingMovieCount}", await existingMovies.CountAsync());
        _logger.LogInformation("New movies: {NewMovieCount}", movieEvents.Count);

        // Update channels
        foreach (var channel in movieEvents.Select(m => m.Channel).Distinct())
        {
            var existingChannel = await _moviesDbContext.Channels.SingleOrDefaultAsync(c => c.Code == channel.Code);
            if (existingChannel != null)
            {
                existingChannel.Name = channel.Name;
                existingChannel.LogoS = channel.LogoS;
                _moviesDbContext.Channels.Update(existingChannel);
                foreach (var movie in movieEvents.Where(m => m.Channel == channel))
                    movie.Channel = existingChannel;
            }
            else
            {
                _moviesDbContext.Channels.Add(channel);
            }
        }

        // Remove exising movies that don't appear in new movies
        {
            var remove = existingMovies.ToList().Where(m1 => movieEvents.All(m2 => m2.ExternalId != m1.ExternalId))
                .ToList();
            _logger.LogInformation("Existing movies to be removed: {ExistingMoviesToRemove}", remove.Count);
            _moviesDbContext.RemoveRange(remove);
            await _moviesDbContext.SaveChangesAsync();
        }

        foreach (var movie in movieEvents)
        {
            var existingDuplicates = await existingMovies
                .Where(me => me.ExternalId == movie.ExternalId)
                .OrderBy(me => me.Id)
                .Skip(1)
                .ToListAsync();
            _moviesDbContext.MovieEvents.RemoveRange(existingDuplicates);
            await _moviesDbContext.SaveChangesAsync();
        }

        // Update movies
        foreach (var movie in movieEvents)
        {
            var existingMovie = await existingMovies.Include(me => me.Movie)
                .SingleOrDefaultAsync(me => me.ExternalId == movie.ExternalId);
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
                existingMovie.AddedTime ??= DateTime.UtcNow;
            }
            else
            {
                movie.AddedTime = DateTime.UtcNow;
                _moviesDbContext.MovieEvents.Add(movie);
            }
        }

        await _moviesDbContext.SaveChangesAsync();
    }

    private async Task UpdateMissingImageLinks()
    {
        foreach (var movieEvent in _moviesDbContext.MovieEvents)
        {
            var emptyPosterM = string.IsNullOrEmpty(movieEvent.PosterM);
            var emptyPosterS = string.IsNullOrEmpty(movieEvent.PosterS);
            if ((emptyPosterM || emptyPosterS) && !string.IsNullOrEmpty(movieEvent.Movie?.ImdbId))
                try
                {
                    var result = await _theMovieDbService.GetImages(movieEvent.Movie.ImdbId);
                    movieEvent.PosterM = result.Medium;
                    movieEvent.PosterS = result.Small;
                }
                catch (Exception x)
                {
                    _logger.LogError(x, "UpdateMissingImageLinks failed for movie {ImdbId}", movieEvent.Movie.ImdbId);
                }
        }

        await _moviesDbContext.SaveChangesAsync();
    }

    private async Task DownloadImageData()
    {
        if (_downloadImagesOption == UpdateEpgCommandOptions.DownloadImagesOption.Disabled)
            return;

        var basePath = _updateEpgCommandOptions.ImageBasePath ?? Environment.CurrentDirectory;
        if (!Directory.Exists(basePath))
            Directory.CreateDirectory(basePath);

        foreach (var channel in _moviesDbContext.Channels)
        {
            var url = channel.LogoS;

            if (url == null)
                continue;

            if (CheckDownloadNotNeeded(channel.LogoS_Local))
            {
                _logger.LogInformation("Skipping existing image {url}, local {local}",
                    url, channel.LogoS_Local);
                continue;
            }

            var name = "channel-" + channel.Code;

            if (_updateEpgCommandOptions.ImageOverrideMap.TryGetValue(name, out var imageOverride))
            {
                var target = Path.Combine(basePath, name);
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

        foreach (var movieEvent in _moviesDbContext.MovieEvents)
        {
            {
                var url = movieEvent.PosterS;

                if (url == null)
                    continue;

                if (CheckDownloadNotNeeded(movieEvent.PosterS_Local))
                {
                    _logger.LogInformation("Skipping existing image {url}, local {local}",
                        url, movieEvent.PosterS_Local);
                    continue;
                }

                var name = "movie-" + movieEvent.Id + "-S";

                movieEvent.PosterS_Local = await DownloadFile(url, basePath, name, 150);
            }

            if (movieEvent.PosterM == movieEvent.PosterS)
            {
                movieEvent.PosterM_Local = movieEvent.PosterS_Local;
            }
            else
            {
                var url = movieEvent.PosterM;

                if (url == null)
                    continue;

                if (CheckDownloadNotNeeded(movieEvent.PosterM_Local))
                {
                    _logger.LogInformation("Skipping existing image {url}, local {local}",
                        url, movieEvent.PosterM_Local);
                    continue;
                }

                var name = "movie-" + movieEvent.Id + "-M";

                movieEvent.PosterM_Local = await DownloadFile(url, basePath, name, 0);
            }
        }

        await _moviesDbContext.SaveChangesAsync();
    }

    private bool CheckDownloadNotNeeded(string localFile)
    {
        if (_downloadImagesOption == UpdateEpgCommandOptions.DownloadImagesOption.Active)
            return false;
        if (_downloadImagesOption == UpdateEpgCommandOptions.DownloadImagesOption.Disabled)
            return true;
        if (_downloadImagesOption == UpdateEpgCommandOptions.DownloadImagesOption.IfNotPresent)
        {
            if (string.IsNullOrEmpty(localFile))
                return false;

            var file = Path.Combine(_updateEpgCommandOptions.ImageBasePath, localFile);
            return File.Exists(file);
        }

        throw new InvalidOperationException();
    }

    private async Task ResizeFile(string imageFile, int width)
    {
        try
        {
            var image = await Image.LoadAsync(imageFile);
            image.Mutate(i => i.Resize(width, 0));
            await image.SaveAsync(imageFile);
        }
        catch (Exception x)
        {
            _logger.LogWarning(x, "Failed to resize {ImageFile}", imageFile);
        }
    }

    private async Task<string> DownloadFile(string url, string basePath, string name, int resize)
    {
        if (string.IsNullOrEmpty(url))
            return null;

        string target;

        try
        {
            var client = _httpClientFactory.CreateClient("images");
            var rsp = await client.GetAsync(url);
            var contentType = rsp.Content.Headers.ContentType?.MediaType;
            var ext = contentType != null &&
                      contentType.Equals("image/png", StringComparison.InvariantCultureIgnoreCase)
                ? ".png"
                : ".jpg";
            name = name + ext;
            target = Path.Combine(basePath, name);
            _logger.LogInformation("Downloading {Url} to {FileName}, {ContentType}",
                url, target, contentType);
            await using var stm = await rsp.Content.ReadAsStreamAsync();
            await using var fileStream = File.Create(target);
            await stm.CopyToAsync(fileStream);
        }
        catch (Exception x)
        {
            _logger.LogWarning(x, "FAILED download of {Url}", url);
            return null;
        }

        if (resize > 0) await ResizeFile(target, resize);

        return name;
    }

    private async Task UpdateEpgDataWithImdb()
    {
        await UpdateGenericDataWithImdb(dbMovies => dbMovies.MovieEvents.Include(me => me.Movie));
    }

    private async Task UpdateGenericDataWithImdb(Func<MoviesDbContext, IQueryable<IHasImdbLink>> fnGetMovies)
    {
        var groups = fnGetMovies(_moviesDbContext).AsEnumerable().GroupBy(m => new { m.Title, m.Year }).ToList();
        var totalCount = groups.Count;
        var current = 0;
        foreach (var group in groups) //.ToList())
        {
            current++;

            if (group.Any(m => m.Movie != null))
            {
                var firstMovieWithImdbLink = group.First(m => m.Movie != null);
                foreach (var other in group.Where(m => m.Movie == null)) other.Movie = firstMovieWithImdbLink.Movie;

                await _moviesDbContext.SaveChangesAsync();
                continue;
            }

            var first = group.First();
            var imdbMatchingQueryResult = await _imdbMatchingQuery.Execute(first.Title, first.Year);
            var imdbMovie = imdbMatchingQueryResult.ImdbMovie;
            var huntNo = imdbMatchingQueryResult.HuntNo;

            Movie movie = null;
            if (imdbMovie == null)
            {
                huntNo = -1;

                var manualMatch = await _manualMatchesQuery.Execute(first.Title);
                movie = manualMatch?.Movie;
                if (movie != null)
                {
                    _logger.LogInformation(
                        "UpdateEpgDataWithImdb: Fallback using ManualMatch for '{Title} ({Year})' to existing Movie {MovieID} {ImdbId}",
                        first.Title, first.Year, movie.Id, movie.ImdbId);

                    imdbMovie = await _imdbDbContext.Movies.FirstOrDefaultAsync(m => m.ImdbId == movie.ImdbId);
                }
            }

            if (movie == null && imdbMovie == null)
            {
                _logger.LogInformation("UpdateEpgDataWithImdb: Could not find movie '{Title} ({Year})' in IMDb",
                    first.Title, first.Year);
                continue;
            }

            _logger.LogInformation("{PercentDone}% {Title} ({Year}) ==> {ImdbId}, duplicity={Duplicity}, HUNT#{HuntNo}",
                100 * current / totalCount, first.Title, first.Year,
                movie?.ImdbId ?? imdbMovie?.ImdbId, group.Count(), huntNo);

            movie ??= await _moviesDbContext.Movies.SingleOrDefaultAsync(m => m.ImdbId == imdbMovie.ImdbId);

            movie ??= new Movie
            {
                ImdbId = imdbMovie.ImdbId
            };

            if (imdbMovie != null)
            {
                movie.ImdbRating = imdbMovie.Rating;
                movie.ImdbVotes = imdbMovie.Votes;
            }

            movie.Certification ??= await _theMovieDbService.GetCertification(movie.ImdbId) ?? "";

            foreach (var movieWithImdbLink in group) movieWithImdbLink.Movie = movie;
        }

        await _moviesDbContext.SaveChangesAsync();
    }
}