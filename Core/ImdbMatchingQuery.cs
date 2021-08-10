using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FxMovies.FxMoviesDB;
using FxMovies.ImdbDB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FxMovies.Core
{
    public interface IImdbMatchingQuery
    {
        Task<ImdbMatchingQueryResult> Run(string movieTitle, int? movieReleaseYear);
        Task<int> RunTest(string movieTitle, int? movieReleaseYear);
    }

    public class ImdbMatchingQueryOptions
    {
        public static string Position => "ImdbMatching";

        public int? ImdbHuntingYearDiff { get; set; }
    }

    public class ImdbMatchingQueryResult
    {
        public ImdbDB.Movie ImdbMovie { get; set; }
        public int HuntNo { get; set; }
    }

    public class ImdbMatchingQuery : IImdbMatchingQuery
    {
        private readonly ILogger<ImdbMatchingQuery> logger;
        private readonly ImdbDbContext imdbDbContext;

        private readonly List<Func<string, int?, IQueryable<ImdbDB.Movie>>> huntingProcedure;

        public ImdbMatchingQuery(ILogger<ImdbMatchingQuery> logger,
            IOptionsSnapshot<ImdbMatchingQueryOptions> imdbMatchingQueryOptions,
            ImdbDbContext imdbDbContext)
        {
            this.logger = logger;
            int imdbHuntingYearDiff = imdbMatchingQueryOptions.Value.ImdbHuntingYearDiff ?? 2;
            this.imdbDbContext = imdbDbContext;

            huntingProcedure = new List<Func<string, int?, IQueryable<ImdbDB.Movie>>>();

            // Search for PrimaryTitle (Year)
            huntingProcedure.Add((Func<string, int?, IQueryable<ImdbDB.Movie>>)
            (
                (title, releaseYear) => {
                    string normalizedTitle = ImdbDB.Util.NormalizeTitle(title);
                    return imdbDbContext.MovieAlternatives
                        .Where(ma =>
                            ma.AlternativeTitle == null 
                            && ma.Normalized == normalizedTitle
                            && (!ma.Movie.Year.HasValue || !releaseYear.HasValue || ma.Movie.Year == releaseYear.Value)
                        ).Select(ma => ma.Movie);
                    }
            ));

            // Search for AlternativeTitle (Year)
            huntingProcedure.Add((Func<string, int?, IQueryable<ImdbDB.Movie>>)
            (
                (title, releaseYear) => {
                    string normalizedTitle = ImdbDB.Util.NormalizeTitle(title);
                    return imdbDbContext.MovieAlternatives
                        .Where(ma => 
                            ma.AlternativeTitle != null 
                            && ma.Normalized == normalizedTitle
                            && (!ma.Movie.Year.HasValue || !releaseYear.HasValue || ma.Movie.Year == releaseYear.Value)
                        ).Select(ma => ma.Movie);
                    }
            ));

            // Search for PrimaryTitle (+/-Year)
            huntingProcedure.Add((Func<string, int?, IQueryable<ImdbDB.Movie>>)
            (
                (title, releaseYear) => {
                    string normalizedTitle = ImdbDB.Util.NormalizeTitle(title);
                    return imdbDbContext.MovieAlternatives
                        .Where(ma =>
                            ma.AlternativeTitle == null 
                            && ma.Normalized == normalizedTitle
                            && (!ma.Movie.Year.HasValue || !releaseYear.HasValue 
                                || ((ma.Movie.Year >= releaseYear.Value - imdbHuntingYearDiff) && (ma.Movie.Year <= releaseYear.Value + imdbHuntingYearDiff)))
                        ).Select(ma => ma.Movie);
                    }
            ));

            // Search for AlternativeTitle (+/-Year)
            huntingProcedure.Add((Func<string, int?, IQueryable<ImdbDB.Movie>>)
            (
                (title, releaseYear) => {
                    string normalizedTitle = ImdbDB.Util.NormalizeTitle(title);
                    return imdbDbContext.MovieAlternatives
                        .Where(ma => 
                            ma.AlternativeTitle != null 
                            && ma.Normalized == normalizedTitle
                            && (!ma.Movie.Year.HasValue || !releaseYear.HasValue 
                                || ((ma.Movie.Year >= releaseYear.Value - imdbHuntingYearDiff) && (ma.Movie.Year <= releaseYear.Value + imdbHuntingYearDiff)))
                        ).Select(ma => ma.Movie);
                    }
            ));
        }

        public async Task<ImdbMatchingQueryResult> Run(string movieTitle, int? movieReleaseYear)
        {
            ImdbDB.Movie imdbMovie = null;
            int huntNo = 0;
            foreach (var hunt in huntingProcedure)
            {                        
                imdbMovie = await hunt(movieTitle, movieReleaseYear)
                    .OrderByDescending(m => m.Votes)
                    .FirstOrDefaultAsync();

                if (imdbMovie != null)
                    break;
            
                huntNo++;
            }

            return new ImdbMatchingQueryResult()
            {
                ImdbMovie = imdbMovie,
                HuntNo = huntNo
            };
        }

        public async Task<int> RunTest(string movieTitle, int? movieReleaseYear)
        {
            var result = await Run(movieTitle, movieReleaseYear);
            var imdbMovie = result.ImdbMovie;
            if (imdbMovie != null)
            {
                logger.LogError("Movie '{MovieTitle}' ({LovieReleaseYear}) found: {ImdbId} - '{PrimaryTitle}' ({Year})", 
                    movieTitle, movieReleaseYear, imdbMovie.ImdbId, imdbMovie.PrimaryTitle, imdbMovie.Year);
                return 0;
            }
            else
            {
                logger.LogError("Movie '{MovieTitle}' ({MovieReleaseYear}) not found", movieTitle, movieReleaseYear);
                return 1;
            }
        }
    }
}