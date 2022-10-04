using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using FxMovies.Core.Entities;
using FxMovies.Core.Utilities;
using FxMovies.ImdbDB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FxMovies.Core.Queries;

public interface IImdbMatchingQuery
{
    Task<ImdbMatchingQueryResult> Execute(string movieTitle, int? movieReleaseYear);
}

[ExcludeFromCodeCoverage]
public class ImdbMatchingQueryOptions
{
    public static string Position => "ImdbMatching";

    public int? ImdbHuntingYearDiff { get; set; }
}

[ExcludeFromCodeCoverage]
public class ImdbMatchingQueryResult
{
    public ImdbMovie ImdbMovie { get; init; }
    public int HuntNo { get; init; }
}

public class ImdbMatchingQuery : IImdbMatchingQuery
{
    private readonly List<Func<string, int?, IQueryable<ImdbMovie>>> _huntingProcedure;
    private readonly ImdbDbContext _imdbDbContext;

    public ImdbMatchingQuery(
        IOptionsSnapshot<ImdbMatchingQueryOptions> imdbMatchingQueryOptions,
        ImdbDbContext imdbDbContext)
    {
        var imdbHuntingYearDiff = imdbMatchingQueryOptions.Value.ImdbHuntingYearDiff ?? 2;
        _imdbDbContext = imdbDbContext;

        _huntingProcedure = new List<Func<string, int?, IQueryable<ImdbMovie>>>
        {
            // Search for PrimaryTitle (Year)
            (title, releaseYear) =>
            {
                var normalizedTitle = TitleNormalizer.NormalizeTitle(title);
                return imdbDbContext.MovieAlternatives
                    .Where(ma =>
                        ma.AlternativeTitle == null
                        && ma.Normalized == normalizedTitle
                        && (!ma.Movie.Year.HasValue || !releaseYear.HasValue || ma.Movie.Year == releaseYear.Value)
                    ).Select(ma => ma.Movie);
            },

            // Search for AlternativeTitle (Year)
            (title, releaseYear) =>
            {
                var normalizedTitle = TitleNormalizer.NormalizeTitle(title);
                return imdbDbContext.MovieAlternatives
                    .Where(ma =>
                        ma.AlternativeTitle != null
                        && ma.Normalized == normalizedTitle
                        && (!ma.Movie.Year.HasValue || !releaseYear.HasValue || ma.Movie.Year == releaseYear.Value)
                    ).Select(ma => ma.Movie);
            },

            // Search for PrimaryTitle (+/-Year)
            (title, releaseYear) =>
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
            },

            // Search for AlternativeTitle (+/-Year)
            (title, releaseYear) =>
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
            }
        };
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