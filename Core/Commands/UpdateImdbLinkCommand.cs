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
    Task Execute(int movieEventId, string imdbId, bool ignoreImdbLink);
}

public class UpdateImdbLinkCommand : IUpdateImdbLinkCommand
{
    private readonly FxMoviesDbContext fxMoviesDbContext;
    private readonly ImdbDbContext imdbDbContext;
    private readonly ILogger<UpdateImdbUserDataCommand> logger;
    private readonly IMovieCreationHelper movieCreationHelper;

    public UpdateImdbLinkCommand(
        ILogger<UpdateImdbUserDataCommand> logger,
        FxMoviesDbContext fxMoviesDbContext,
        ImdbDbContext imdbDbContext,
        IMovieCreationHelper movieCreationHelper)
    {
        this.logger = logger;
        this.fxMoviesDbContext = fxMoviesDbContext;
        this.imdbDbContext = imdbDbContext;
        this.movieCreationHelper = movieCreationHelper;
    }

    public async Task Execute(int movieEventId, string imdbId, bool ignoreImdbLink)
    {
        var movieEvent = await fxMoviesDbContext.MovieEvents
            .Include(me => me.Movie)
            .Include(me => me.Channel)
            .SingleOrDefaultAsync(me => me.Id == movieEventId);
        if (movieEvent != null)
        {
            if (movieEvent.Movie != null && movieEvent.Movie.ImdbId == imdbId &&
                movieEvent.Movie.ImdbIgnore != ignoreImdbLink)
            {
                // Already ok, Do nothing
                logger.LogInformation("Skipped saving {ImdbId}, no changes", imdbId);
            }
            else
            {
                Movie movie;
                if (imdbId != null)
                    movie = await movieCreationHelper.GetOrCreateMovieByImdbId(imdbId);
                else
                    movie = new Movie();

                movieEvent.Movie = movie;
                movie.ImdbIgnore = ignoreImdbLink;

                if (imdbId != null)
                {
                    var imdbMovie = await imdbDbContext.Movies.SingleOrDefaultAsync(m => m.ImdbId == imdbId);
                    if (imdbMovie != null)
                    {
                        movieEvent.Movie.ImdbRating = imdbMovie.Rating;
                        movieEvent.Movie.ImdbVotes = imdbMovie.Votes;
                        if (!movieEvent.Year.HasValue)
                            movieEvent.Year = imdbMovie.Year;
                    }
                }

                fxMoviesDbContext.ManualMatches.Add(
                    new ManualMatch
                    {
                        AddedDateTime = DateTime.UtcNow,
                        Movie = movie,
                        Title = movieEvent.Title,
                        NormalizedTitle = TitleNormalizer.NormalizeTitle(movieEvent.Title)
                    }
                );
            }

            await fxMoviesDbContext.SaveChangesAsync();
        }
    }
}