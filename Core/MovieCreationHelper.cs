using System.Linq;

namespace FxMovies.Core
{
    public interface IMovieCreationHelper
    {
        FxMovies.FxMoviesDB.Movie GetOrCreateMovieByImdbId(string imdbId, bool refresh = false);
    }

    public class MovieCreationHelper : IMovieCreationHelper
    {
        private readonly FxMovies.FxMoviesDB.FxMoviesDbContext fxMoviesDbContext;
        private readonly FxMovies.ImdbDB.ImdbDbContext imdbDbContext;
        private readonly ITheMovieDbService theMovieDbService;

        public MovieCreationHelper(
            FxMovies.FxMoviesDB.FxMoviesDbContext fxMoviesDbContext, 
            FxMovies.ImdbDB.ImdbDbContext imdbDbContext,
            ITheMovieDbService theMovieDbService)
        {
            this.fxMoviesDbContext = fxMoviesDbContext;
            this.imdbDbContext = imdbDbContext;
            this.theMovieDbService = theMovieDbService;
        }

        public FxMovies.FxMoviesDB.Movie GetOrCreateMovieByImdbId(string imdbId, bool refresh = false)
        {
            var movie = fxMoviesDbContext.Movies.SingleOrDefault(m => m.ImdbId == imdbId);

            bool newMovie = (movie == null);

            if (newMovie)
            {
                movie = new FxMoviesDB.Movie()
                {
                    ImdbId = imdbId
                };
                fxMoviesDbContext.Movies.Add(movie);
            }

            if (refresh)
                Refresh(movie);

            return movie;
        }

        public bool RefreshIfNeeded(FxMovies.FxMoviesDB.Movie movie)
        {
            if (string.IsNullOrEmpty(movie.OriginalTitle))
            {
                return Refresh(movie);
            }
            return false;
        }

        public bool Refresh(FxMovies.FxMoviesDB.Movie movie)
        {
            if (movie == null)
                return false;

            var imdbMovie = imdbDbContext.Movies.SingleOrDefault(m => m.ImdbId == movie.ImdbId);
            if (imdbMovie != null)
            {
                movie.ImdbRating = imdbMovie.Rating;
                movie.ImdbVotes = imdbMovie.Votes;
                if (movie.Certification == null)
                    movie.Certification = theMovieDbService.GetCertification(movie.ImdbId) ?? "";

                return true;
            }

            return false;
        }
    }
}