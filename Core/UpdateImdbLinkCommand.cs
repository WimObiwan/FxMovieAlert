using System.Threading.Tasks;
using FxMovies.FxMoviesDB;
using FxMovies.ImdbDB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FxMovies.Core
{
    public interface IUpdateImdbLinkCommand
    {
        Task Run(int movieEventId, string imdbId, bool ignoreImdbLink);
    }

    public class UpdateImdbLinkCommand : IUpdateImdbLinkCommand
    {
        private readonly ILogger<UpdateImdbUserDataCommand> logger;
        private readonly FxMoviesDbContext fxMoviesDbContext;
        private readonly ImdbDbContext imdbDbContext;
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

        public async Task Run(int movieEventId, string imdbId, bool ignoreImdbLink)
        {
            var movieEvent = await fxMoviesDbContext.MovieEvents
                .Include(me => me.Movie)
                .Include(me => me.Channel)
                .SingleOrDefaultAsync(me => me.Id == movieEventId);
            if (movieEvent != null)
            {
                if (movieEvent.Movie != null && movieEvent.Movie.ImdbId == imdbId && movieEvent.Movie.ImdbIgnore != ignoreImdbLink)
                {
                    // Already ok, Do nothing
                    logger.LogInformation("Skipped saving {ImdbId}, no changes", imdbId);
                }
                else
                {
                    FxMovies.FxMoviesDB.Movie movie;
                    if (imdbId != null)
                        movie = await movieCreationHelper.GetOrCreateMovieByImdbId(imdbId);
                    else
                        movie = new FxMovies.FxMoviesDB.Movie();

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
                        new ManualMatch()
                        {
                            Movie = movie,
                            Title = movieEvent.Title,
                            NormalizedTitle = ImdbDB.Util.NormalizeTitle(movieEvent.Title)
                        }
                    );
                }

                await fxMoviesDbContext.SaveChangesAsync();
            } 
        }
   }
}