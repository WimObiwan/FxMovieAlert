using System.Threading.Tasks;
using FxMovies.Core.Entities;
using FxMovies.Core.Services;
using FxMovies.FxMoviesDB;
using FxMovies.ImdbDB;
using Microsoft.EntityFrameworkCore;

namespace FxMovies.Core;

public interface IMovieCreationHelper
{
    Task<Movie> GetOrCreateMovieByImdbId(string imdbId, bool refresh = false);
}

public class MovieCreationHelper : IMovieCreationHelper
{
    private readonly FxMoviesDbContext fxMoviesDbContext;
    private readonly ImdbDbContext imdbDbContext;
    private readonly ITheMovieDbService theMovieDbService;

    public MovieCreationHelper(
        FxMoviesDbContext fxMoviesDbContext,
        ImdbDbContext imdbDbContext,
        ITheMovieDbService theMovieDbService)
    {
        this.fxMoviesDbContext = fxMoviesDbContext;
        this.imdbDbContext = imdbDbContext;
        this.theMovieDbService = theMovieDbService;
    }

    public async Task<Movie> GetOrCreateMovieByImdbId(string imdbId, bool refresh = false)
    {
        var movie = await fxMoviesDbContext.Movies.SingleOrDefaultAsync(m => m.ImdbId == imdbId);

        var newMovie = movie == null;

        if (newMovie)
        {
            movie = new Movie
            {
                ImdbId = imdbId
            };
            fxMoviesDbContext.Movies.Add(movie);
        }

        if (refresh)
            await Refresh(movie);

        return movie;
    }

    public async Task<bool> RefreshIfNeeded(Movie movie)
    {
        if (string.IsNullOrEmpty(movie.OriginalTitle)) return await Refresh(movie);
        return false;
    }

    public async Task<bool> Refresh(Movie movie)
    {
        if (movie == null)
            return false;

        var imdbMovie = await imdbDbContext.Movies.SingleOrDefaultAsync(m => m.ImdbId == movie.ImdbId);
        if (imdbMovie != null)
        {
            movie.ImdbRating = imdbMovie.Rating;
            movie.ImdbVotes = imdbMovie.Votes;
            if (movie.Certification == null)
                movie.Certification = await theMovieDbService.GetCertification(movie.ImdbId) ?? "";

            return true;
        }

        return false;
    }
}