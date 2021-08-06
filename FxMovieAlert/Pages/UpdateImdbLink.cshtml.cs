using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FxMovies.Core;
using FxMovies.FxMoviesDB;
using FxMovies.ImdbDB;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FxMovieAlert.Pages
{
    public class UpdateImdbLinkModel : PageModel
    {
        private readonly ILogger<UpdateImdbLinkModel> logger;
        private readonly FxMoviesDbContext fxMoviesDbContext;
        private readonly ImdbDbContext imdbDbContext;
        private readonly IMovieCreationHelper movieCreationHelper;

        public UpdateImdbLinkModel(
            ILogger<UpdateImdbLinkModel> logger,
            FxMoviesDbContext fxMoviesDbContext,
            ImdbDbContext imdbDbContext,
            IMovieCreationHelper movieCreationHelper)
        {
            this.logger = logger;
            this.fxMoviesDbContext = fxMoviesDbContext;
            this.imdbDbContext = imdbDbContext;
            this.movieCreationHelper = movieCreationHelper;
        }

        public IActionResult OnGet()
        {
            return RedirectToPage("Index");
        }

        public async Task<IActionResult> OnPostAsync(int? movieeventid, string setimdbid,
            string returnPage)
        {
            var editImdbLinks = ClaimChecker.Has(User.Identity, Claims.EditImdbLinks);

            if (editImdbLinks && movieeventid.HasValue && !string.IsNullOrEmpty(setimdbid))
            {
                bool overwrite = false;
                bool setIgnore = false;
                var match = Regex.Match(setimdbid, @"(tt\d+)");
                if (match.Success)
                {
                    setimdbid = match.Groups[0].Value;
                    overwrite = true;
                }
                else if (setimdbid.Equals("ignore", StringComparison.InvariantCultureIgnoreCase))
                {
                    setimdbid = null;
                    overwrite = true;
                    setIgnore = true;
                }
                else if (setimdbid.Equals("remove", StringComparison.InvariantCultureIgnoreCase))
                {
                    setimdbid = null;
                    overwrite = true;
                }
                
                if (overwrite)
                {
                    var movieEvent = await fxMoviesDbContext.MovieEvents
                        .Include(me => me.Movie)
                        .Include(me => me.Channel)
                        .SingleOrDefaultAsync(me => me.Id == movieeventid.Value);
                    if (movieEvent != null)
                    {
                        if (movieEvent.Movie != null && movieEvent.Movie.ImdbId == setimdbid && movieEvent.Movie.ImdbIgnore != setIgnore)
                        {
                            // Already ok, Do nothing
                            logger.LogInformation("Skipped saving {ImdbId}, no changes", setimdbid);
                        }
                        else
                        {
                            FxMovies.FxMoviesDB.Movie movie;
                            if (setimdbid != null)
                                movie = await movieCreationHelper.GetOrCreateMovieByImdbId(setimdbid);
                            else
                                movie = new FxMovies.FxMoviesDB.Movie();

                            movieEvent.Movie = movie;
                            movie.ImdbIgnore = setIgnore;

                            if (setimdbid != null)
                            {
                                var imdbMovie = await imdbDbContext.Movies.SingleOrDefaultAsync(m => m.ImdbId == setimdbid);
                                if (imdbMovie != null)
                                {
                                    movieEvent.Movie.ImdbRating = imdbMovie.Rating;
                                    movieEvent.Movie.ImdbVotes = imdbMovie.Votes;
                                    if (!movieEvent.Year.HasValue)
                                        movieEvent.Year = imdbMovie.Year;
                                }
                            }
                        }

                        await fxMoviesDbContext.SaveChangesAsync();
                    } 
                }
            }
            
            return Redirect(returnPage);
        }
    }
}
