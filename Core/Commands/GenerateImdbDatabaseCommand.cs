using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FxMovies.Core.Entities;
using FxMovies.Core.Utilities;
using FxMovies.ImdbDB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FxMovies.Core.Commands;

public interface IGenerateImdbDatabaseCommand
{
    Task<int> Execute();
}

public class GenerateImdbDatabaseCommandOptions
{
    public static string Position => "GenerateImdbDatabase";

    public int? DebugMaxImdbRowCount { get; set; }
    public string? ImdbMoviesList { get; set; }
    public string? ImdbAlsoKnownAsList { get; set; }
    public string? ImdbRatingsList { get; set; }
    public string[]? AkaFilterRegion { get; set; }
    public string[]? AkaFilterLanguage { get; set; }
}

public class GenerateImdbDatabaseCommand : IGenerateImdbDatabaseCommand
{
    private const int BatchSize = 10000;
    private readonly GenerateImdbDatabaseCommandOptions _generateImdbDatabaseCommandOptions;
    private readonly ILogger<UpdateEpgCommand> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public GenerateImdbDatabaseCommand(ILogger<UpdateEpgCommand> logger,
        IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<GenerateImdbDatabaseCommandOptions> generateImdbDatabaseCommandOptions)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
        _generateImdbDatabaseCommandOptions = generateImdbDatabaseCommandOptions.Value;
    }

    public async Task<int> Execute()
    {
        await ImportImdbData_Remove();
        await ImportImdbData_Movies();
        await ImportImdbData_AlsoKnownAs();
        await ImportImdbData_Ratings();
        await ImportImdbData_CleanupMoviesWithoutRatings();
        await ImportImdbData_Vacuum();
        return 0;
    }

    private async Task ImportImdbData_Remove()
    {
        _logger.LogInformation("Removing MovieAlternatives");
        do
        {
            using var scope = _serviceScopeFactory.CreateScope();
            await using var db = scope.ServiceProvider.GetRequiredService<ImdbDbContext>();
            var batch = db.MovieAlternatives.AsNoTracking().OrderBy(ma => ma.Id).Take(BatchSize);
            if (!batch.Any())
                break;
            db.MovieAlternatives.RemoveRange(batch);
            await db.SaveChangesAsync();
        } while (true);

        _logger.LogInformation("Removing Movies");
        do
        {
            using var scope = _serviceScopeFactory.CreateScope();
            await using var db = scope.ServiceProvider.GetRequiredService<ImdbDbContext>();
            var batch = db.Movies.AsNoTracking().OrderBy(m => m.Id).Take(BatchSize);
            if (!batch.Any())
                break;
            db.Movies.RemoveRange(batch);
            await db.SaveChangesAsync();
        } while (true);
    }

    private async Task ImportImdbData_Movies()
    {
        var debugMaxImdbRowCount = _generateImdbDatabaseCommandOptions.DebugMaxImdbRowCount ?? 0;

        var fileToDecompress = new FileInfo(_generateImdbDatabaseCommandOptions.ImdbMoviesList);
        await using var originalFileStream = fileToDecompress.OpenRead();
        await using var decompressionStream = new GZipStream(originalFileStream, CompressionMode.Decompress);
        using var textReader = new StreamReader(decompressionStream);
        long count = 0, countAlternatives = 0, skipped = 0;
        string? text = null;

        // tconst	titleType	primaryTitle	originalTitle	isAdult	startYear	endYear	runtimeMinutes	genres
        // tt0000009	movie	Miss Jerry	Miss Jerry	0	1894	\N	45	Romance
        var regex = new Regex(@"^([^\t]*)\t([^\t]*)\t([^\t]*)\t([^\t]*)\t[^\t]*\t([^\t]*)\t[^\t]*\t[^\t]*\t[^\t]*$",
            RegexOptions.Compiled);

        var stopwatch = new Stopwatch();
        stopwatch.Start();

        // Skip header
        await textReader.ReadLineAsync();

        var filterTypes = new[]
        {
            "movie",
            "video",
            "short",
            "tvMovie",
            "tvMiniSeries",
            "tvSeries"
        };

        do
        {
            using var scope = _serviceScopeFactory.CreateScope();
            await using var db = scope.ServiceProvider.GetRequiredService<ImdbDbContext>();
            var batchCount = 0;
            while (batchCount < BatchSize && (text = await textReader.ReadLineAsync()) != null)
            {
                batchCount++;
                count++;

                if (count % BatchSize == 0)
                {
                    _logger.LogInformation(
                        "UpdateImdbDataWithMovies: {CountDone} records done ({PercentDone}%), "
                        + "{CountAlternatives} alternatives, {CountSkipped} records skipped ({PercentSkipped}%), {msecs}",
                        count, originalFileStream.Position * 100 / originalFileStream.Length,
                        countAlternatives, skipped, skipped * 100 / count,
                        stopwatch.ElapsedMilliseconds);
                    stopwatch.Restart();

                    // For debugging
                    if (debugMaxImdbRowCount > 0 && count >= debugMaxImdbRowCount)
                        break;
                }

                var match = regex.Match(text);
                if (!match.Success)
                {
                    _logger.LogWarning("Unable to parse line {LineNo}: {LineText}", count, text);
                    continue;
                }

                // Filter on movie|video|short|tvMovie|tvMiniSeries
                if (!filterTypes.Contains(match.Groups[2].Value))
                {
                    skipped++;
                    continue;
                }

                ImdbMovie movie = new()
                {
                    ImdbId = match.Groups[1].Value,
                    PrimaryTitle = match.Groups[3].Value
                };

                var originalTitle = match.Groups[4].Value;

                if (int.TryParse(match.Groups[5].Value, out var startYear))
                    movie.Year = startYear;

                var primaryTitleNormalized = TitleNormalizer.NormalizeTitle(movie.PrimaryTitle);

                countAlternatives++;
                movie.MovieAlternatives = new List<ImdbMovieAlternative>
                {
                    new()
                    {
                        Movie = movie,
                        AlternativeTitle = null,
                        Normalized = primaryTitleNormalized
                    }
                };

                var originalTitleNormalized = TitleNormalizer.NormalizeTitle(originalTitle);
                if (primaryTitleNormalized != originalTitleNormalized)
                {
                    countAlternatives++;
                    movie.MovieAlternatives.Add(
                        new ImdbMovieAlternative
                        {
                            Movie = movie,
                            AlternativeTitle = originalTitle,
                            Normalized = originalTitleNormalized
                        });
                }

                db.Movies.Add(movie);
            }

            await db.SaveChangesAsync();
        } while (text != null);

        _logger.LogInformation("IMDb movies scanned: {Count}", count);
    }

    private async Task ImportImdbData_AlsoKnownAs()
    {
        var debugMaxImdbRowCount = _generateImdbDatabaseCommandOptions.DebugMaxImdbRowCount ?? 0;
        var akaFilterRegion = _generateImdbDatabaseCommandOptions.AkaFilterRegion;
        var akaFilterLanguage = _generateImdbDatabaseCommandOptions.AkaFilterLanguage;

        var fileToDecompress = new FileInfo(_generateImdbDatabaseCommandOptions.ImdbAlsoKnownAsList);
        await using var originalFileStream = fileToDecompress.OpenRead();
        await using var decompressionStream = new GZipStream(originalFileStream, CompressionMode.Decompress);
        using var textReader = new StreamReader(decompressionStream);
        long count = 0, countAlternatives = 0, skipped = 0;
        string text = null;

        // 1        2           3       4       5           6       7           8
        // titleId	ordering	title	region	language	types	attributes	isOriginalTitle
        // tt0000001	1	Carmencita - spanyol tánc	HU	\N	imdbDisplay	\N	0
        // "tt0033100\t3\tLilla lögnerskan\tSE\t\\N\t\\N\t\\N\t0"
        //                       (1)       2       (3)       (4)       (5)       6       7       8
        var regex = new Regex(@"^([^\t]*)\t[^\t]*\t([^\t]*)\t([^\t]*)\t([^\t]*)\t[^\t]*\t[^\t]*\t[^\t]*$",
            RegexOptions.Compiled);

        var stopwatch = new Stopwatch();
        stopwatch.Start();

        // Skip header
        await textReader.ReadLineAsync();

        do
        {
            using var scope = _serviceScopeFactory.CreateScope();
            await using var db = scope.ServiceProvider.GetRequiredService<ImdbDbContext>();
            var batchCount = 0;
            while (batchCount < BatchSize && (text = await textReader.ReadLineAsync()) != null)
            {
                batchCount++;
                count++;

                if (count % BatchSize == 0)
                {
                    await db.SaveChangesAsync();
                    _logger.LogInformation(
                        "UpdateImdbDataWithAkas: {CountDone} records done ({PercentDone}%), "
                        + "{CountAlternatives} alternatives, {CountSkipped} records skipped ({PercentSkipped}%), {msecs}",
                        count, originalFileStream.Position * 100 / originalFileStream.Length,
                        countAlternatives, skipped, skipped * 100 / count,
                        stopwatch.ElapsedMilliseconds);
                    stopwatch.Restart();

                    // For debugging
                    if (debugMaxImdbRowCount > 0 && count >= debugMaxImdbRowCount)
                        break;
                }

                var match = regex.Match(text);
                if (!match.Success)
                {
                    _logger.LogWarning("Unable to parse line {LineNo}: {LineText}", count, text);
                    continue;
                }

                if (!(
                        akaFilterRegion != null && akaFilterRegion.Contains(match.Groups[3].Value)
                        || akaFilterLanguage != null && akaFilterLanguage.Contains(match.Groups[4].Value)
                    ))
                {
                    skipped++;
                    continue;
                }

                var movieId = match.Groups[1].Value;
                var movie = await db.Movies.Include(m => m.MovieAlternatives)
                    .SingleOrDefaultAsync(m => m.ImdbId == movieId);
                if (movie == null)
                {
                    skipped++;
                    continue;
                }

                var alternativeTitle = match.Groups[2].Value;

                var normalized = TitleNormalizer.NormalizeTitle(alternativeTitle);
                // Not in DB
                if (movie.MovieAlternatives.Any(ma => ma.Normalized == normalized))
                {
                    skipped++;
                    continue;
                }

                db.MovieAlternatives.Add(
                    new ImdbMovieAlternative
                    {
                        Movie = movie,
                        AlternativeTitle = alternativeTitle,
                        Normalized = normalized
                    }
                );
                countAlternatives++;
            }

            await db.SaveChangesAsync();
        } while (text != null);

        _logger.LogInformation("IMDb movies scanned: {Count}", count);
    }

    private async Task ImportImdbData_Ratings()
    {
        var debugMaxImdbRowCount = _generateImdbDatabaseCommandOptions.DebugMaxImdbRowCount ?? 0;

        var fileToDecompress = new FileInfo(_generateImdbDatabaseCommandOptions.ImdbRatingsList);
        await using var originalFileStream = fileToDecompress.OpenRead();
        await using var decompressionStream = new GZipStream(originalFileStream, CompressionMode.Decompress);
        using var textReader = new StreamReader(decompressionStream);
        long count = 0, skipped = 0;
        string? text = null;

        // New  Distribution  Votes  Rank  Title
        //       0000000125  1852213   9.2  The Shawshank Redemption (1994)
        var regex = new Regex(@"^([^\t]*)\t(\d+\.\d+)\t(\d*)$",
            RegexOptions.Compiled);

        var stopwatch = new Stopwatch();
        stopwatch.Start();

        // Skip header
        await textReader.ReadLineAsync();

        do
        {
            using var scope = _serviceScopeFactory.CreateScope();
            await using var db = scope.ServiceProvider.GetRequiredService<ImdbDbContext>();
            var batchCount = 0;
            while (batchCount < BatchSize && (text = await textReader.ReadLineAsync()) != null)
            {
                batchCount++;
                count++;

                if (count % BatchSize == 0)
                {
                    await db.SaveChangesAsync();
                    _logger.LogInformation(
                        "UpdateImdbDataWithRatings: {CountDone} records done ({PercentDone}%), "
                        + "{CountSkipped} records skipped ({PercentSkipped}%), {msecs}",
                        count, originalFileStream.Position * 100 / originalFileStream.Length,
                        skipped, skipped * 100 / count,
                        stopwatch.ElapsedMilliseconds);
                    stopwatch.Restart();

                    // For debugging
                    if (debugMaxImdbRowCount > 0 && count >= debugMaxImdbRowCount)
                        break;
                }

                var match = regex.Match(text);
                if (!match.Success)
                {
                    _logger.LogWarning("Unable to parse line {LineNo}: {LineText}", count, text);
                    continue;
                }

                var tconst = match.Groups[1].Value;

                var movie = await db.Movies.SingleOrDefaultAsync(m => m.ImdbId == tconst);
                if (movie == null)
                {
                    // Probably a serie or ...
                    //logger.LogWarning("Unable to find movie {0}", tconst);
                    skipped++;
                    continue;
                }

                if (int.TryParse(match.Groups[3].Value, out var votes))
                    movie.Votes = votes;

                if (decimal.TryParse(match.Groups[2].Value, NumberStyles.AllowDecimalPoint,
                        CultureInfo.InvariantCulture.NumberFormat, out var rating))
                    movie.Rating = (int)(rating * 10);
            }

            await db.SaveChangesAsync();
        } while (text != null);

        _logger.LogInformation("IMDb ratings scanned: {Count}", count);
    }

    private async Task ImportImdbData_CleanupMoviesWithoutRatings()
    {
        var year = DateTime.Now.Year - 2;

        _logger.LogInformation("Removing Movies without Rating");

        int total;
        using (var scope = _serviceScopeFactory.CreateScope())
        await using (var db = scope.ServiceProvider.GetRequiredService<ImdbDbContext>())
        {
            total = db.Movies.Where(m => !m.Rating.HasValue && m.Year <= year).OrderBy(m => m.Id).Count();
        }

        _logger.LogInformation("Removing {Count} Movies without Rating", total);
        long count = 0;
        do
        {
            using var scope = _serviceScopeFactory.CreateScope();
            await using var db = scope.ServiceProvider.GetRequiredService<ImdbDbContext>();
            var batch = db.Movies
                .AsNoTracking()
                .Where(m => !m.Rating.HasValue && m.Year <= year)
                .OrderBy(m => m.Id)
                .Take(BatchSize)
                .Include(m => m.MovieAlternatives);
            count += batch.Count();
            if (!batch.Any())
                break;
            // This also removes Alternatives
            db.Movies.RemoveRange(batch);
            await db.SaveChangesAsync();
            _logger.LogInformation("Removing {TotalCount} Movies without Rating, {PercentDone}%", total,
                count * 100 / total);
        } while (true);
    }

    private async Task ImportImdbData_Vacuum()
    {
        using var scope = _serviceScopeFactory.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<ImdbDbContext>();
        _logger.LogInformation("Doing 'VACUUM'...");
        await db.Database.ExecuteSqlRawAsync("VACUUM;");
    }
}