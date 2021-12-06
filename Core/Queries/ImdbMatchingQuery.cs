using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FxMovies.Core.Entities;
using FxMovies.Core.Utilities;
using FxMovies.ImdbDB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FxMovies.Core.Queries;

public interface IImdbMatchingQuery
{
    Task<ImdbMatchingQueryResult> Execute(string movieTitle, int? movieReleaseYear);
}

public class ImdbMatchingQueryOptions
{
    public static string Position => "ImdbMatching";

    public int? ImdbHuntingYearDiff { get; set; }
}

public class ImdbMatchingQueryResult
{
    public ImdbMovie ImdbMovie { get; set; }
    public int HuntNo { get; set; }
}

public class ImdbMatchingQuery : IImdbMatchingQuery
{
    private readonly List<Func<string, int?, IQueryable<ImdbMovie>>> _huntingProcedure;
    private readonly ImdbDbContext _imdbDbContext;
    private readonly ILogger<ImdbMatchingQuery> _logger;

    public ImdbMatchingQuery(ILogger<ImdbMatchingQuery> logger,
        IOptionsSnapshot<ImdbMatchingQueryOptions> imdbMatchingQueryOptions,
        ImdbDbContext imdbDbContext)
    {
        _logger = logger;
        var imdbHuntingYearDiff = imdbMatchingQueryOptions.Value.ImdbHuntingYearDiff ?? 2;
        _imdbDbContext = imdbDbContext;

        _huntingProcedure = new List<Func<string, int?, IQueryable<ImdbMovie>>>();

        // Search for PrimaryTitle (Year)
        _huntingProcedure.Add((title, releaseYear) =>
        {
            var normalizedTitle = TitleNormalizer.NormalizeTitle(title);
            return imdbDbContext.MovieAlternatives
                .Where(ma =>
                    ma.AlternativeTitle == null
                    && ma.Normalized == normalizedTitle
                    && (!ma.Movie.Year.HasValue || !releaseYear.HasValue || ma.Movie.Year == releaseYear.Value)
                ).Select(ma => ma.Movie);
        });

        // Search for AlternativeTitle (Year)
        _huntingProcedure.Add((title, releaseYear) =>
        {
            var normalizedTitle = TitleNormalizer.NormalizeTitle(title);
            return imdbDbContext.MovieAlternatives
                .Where(ma =>
                    ma.AlternativeTitle != null
                    && ma.Normalized == normalizedTitle
                    && (!ma.Movie.Year.HasValue || !releaseYear.HasValue || ma.Movie.Year == releaseYear.Value)
                ).Select(ma => ma.Movie);
        });

        // Search for PrimaryTitle (+/-Year)
        _huntingProcedure.Add((title, releaseYear) =>
        {
            var normalizedTitle = TitleNormalizer.NormalizeTitle(title);
            if (!releaseYear.HasValue)
                return null;
            return imdbDbContext.MovieAlternatives
                .Where(ma =>
                    ma.AlternativeTitle == null
                    && ma.Normalized == normalizedTitle
                    && ma.Movie.Year.HasValue
                    && ma.Movie.Year >= releaseYear.Value - imdbHuntingYearDiff &&
                    ma.Movie.Year <= releaseYear.Value + imdbHuntingYearDiff
                ).Select(ma => ma.Movie);
        });

        // Search for AlternativeTitle (+/-Year)
        _huntingProcedure.Add((title, releaseYear) =>
        {
            var normalizedTitle = TitleNormalizer.NormalizeTitle(title);
            if (!releaseYear.HasValue)
                return null;
            return imdbDbContext.MovieAlternatives
                .Where(ma =>
                    ma.AlternativeTitle != null
                    && ma.Normalized == normalizedTitle
                    && ma.Movie.Year.HasValue
                    && ma.Movie.Year >= releaseYear.Value - imdbHuntingYearDiff &&
                    ma.Movie.Year <= releaseYear.Value + imdbHuntingYearDiff
                ).Select(ma => ma.Movie);
        });
    }

    public async Task<ImdbMatchingQueryResult> Execute(string movieTitle, int? movieReleaseYear)
    {
        ImdbMovie imdbMovie = null;
        var huntNo = 0;
        foreach (var hunt in _huntingProcedure)
        {
            var huntResult = hunt(movieTitle, movieReleaseYear);

            if (huntResult != null)
                imdbMovie = await huntResult
                    .OrderBy(m => Math.Abs((m.Year ?? 0) - (movieReleaseYear ?? m.Year ?? 0)))
                    .ThenByDescending(m => m.Votes)
                    .FirstOrDefaultAsync();

            if (imdbMovie != null)
                break;

            huntNo++;
        }

        return new ImdbMatchingQueryResult
        {
            ImdbMovie = imdbMovie,
            HuntNo = huntNo
        };
    }
}