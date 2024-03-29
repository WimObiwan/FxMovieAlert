using System;
using System.Threading.Tasks;
using FxMovies.Core.Entities;
using FxMovies.Core.Utilities;
using FxMovies.ImdbDB;
using FxMovies.MoviesDB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FxMovies.Core.Commands;

public interface IUpdateImdbLinkCommand
{
    Task Execute(int movieEventId, string? imdbId, bool ignoreImdbLink);
}

public class UpdateImdbLinkCommand : IUpdateImdbLinkCommand
{
    private readonly ImdbDbContext _imdbDbContext;
    private readonly ILogger<UpdateImdbUserDataCommand> _logger;
    private readonly IMovieCreationHelper _movieCreationHelper;
    private readonly MoviesDbContext _moviesDbContext;

    public UpdateImdbLinkCommand(
        ILogger<UpdateImdbUserDataCommand> logger,
        MoviesDbContext moviesDbContext,
        ImdbDbContext imdbDbContext,
        IMovieCreationHelper movieCreationHelper)
    {
        _logger = logger;
        _moviesDbContext = moviesDbContext;
        _imdbDbContext = imdbDbContext;
        _movieCreationHelper = movieCreationHelper;
    }

    public async Task Execute(int movieEventId, string? imdbId, bool ignoreImdbLink)
    {
        var movieEvent = await _moviesDbContext.MovieEvents
            .Include(me => me.Movie)
            .Include(me => me.Channel)
            .SingleOrDefaultAsync(me => me.Id == movieEventId);
        if (movieEvent != null)
        {
            Movie? movie;
            if (imdbId != null)
                movie = await _movieCreationHelper.GetOrCreateMovieByImdbId(imdbId);
            else
                movie = null;

            if (movieEvent.Movie?.Id == movie?.Id
                && (movie == null || movie.ImdbIgnore == ignoreImdbLink)
                && (movieEvent.Ignore == ignoreImdbLink))
            {
                // Already ok, Do nothing
                _logger.LogInformation("Skipped saving {ImdbId}, no changes", imdbId);
            }
            else
            {
                movieEvent.Movie = movie;
                movieEvent.Ignore = ignoreImdbLink;
                if (movie != null)
                {
                    movie.ImdbIgnore = ignoreImdbLink;
                }

                if (imdbId != null)
                {
                    var imdbMovie = await _imdbDbContext.Movies.SingleOrDefaultAsync(m => m.ImdbId == imdbId);
                    if (imdbMovie != null)
                    {
                        if (movieEvent.Movie != null)
                        {
                            movieEvent.Movie.ImdbRating = imdbMovie.Rating;
                            movieEvent.Movie.ImdbVotes = imdbMovie.Votes;
                        }
                        movieEvent.Year ??= imdbMovie.Year;
                    }
                }

                _moviesDbContext.ManualMatches.Add(
                    new ManualMatch
                    {
                        AddedDateTime = DateTime.UtcNow,
                        Movie = movie,
                        Title = movieEvent.Title,
                        NormalizedTitle = movieEvent.Title == null
                            ? null
                            : TitleNormalizer.NormalizeTitle(movieEvent.Title)
                    }
                );
            }

            await _moviesDbContext.SaveChangesAsync();
        }
    }
}