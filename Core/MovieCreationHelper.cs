using System.Threading.Tasks;
using FxMovies.Core.Entities;
using FxMovies.Core.Services;
using FxMovies.ImdbDB;
using FxMovies.MoviesDB;
using Microsoft.EntityFrameworkCore;

namespace FxMovies.Core;

public interface IMovieCreationHelper
{
    Task<Movie> GetOrCreateMovieByImdbId(string imdbId, bool refresh = false);
}

public class MovieCreationHelper : IMovieCreationHelper
{
    private readonly ImdbDbContext _imdbDbContext;
    private readonly MoviesDbContext _moviesDbContext;
    private readonly ITheMovieDbService _theMovieDbService;

    public MovieCreationHelper(
        MoviesDbContext moviesDbContext,
        ImdbDbContext imdbDbContext,
        ITheMovieDbService theMovieDbService)
    {
        _moviesDbContext = moviesDbContext;
        _imdbDbContext = imdbDbContext;
        _theMovieDbService = theMovieDbService;
    }

    public async Task<Movie> GetOrCreateMovieByImdbId(string imdbId, bool refresh = false)
    {
        var movie = await _moviesDbContext.Movies.SingleOrDefaultAsync(m => m.ImdbId == imdbId);

        var newMovie = movie == null;

        if (newMovie)
        {
            movie = new Movie
            {
                ImdbId = imdbId
            };
            _moviesDbContext.Movies.Add(movie);
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

        var imdbMovie = await _imdbDbContext.Movies.SingleOrDefaultAsync(m => m.ImdbId == movie.ImdbId);
        if (imdbMovie != null)
        {
            movie.ImdbRating = imdbMovie.Rating;
            movie.ImdbVotes = imdbMovie.Votes;
            if (movie.Certification == null)
                movie.Certification = await _theMovieDbService.GetCertification(movie.ImdbId) ?? "";

            return true;
        }

        return false;
    }
}