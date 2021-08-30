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
using FxMovies.Core.Services;
using FxMovies.Core.Utilities;
using FxMovies.ImdbDB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FxMovies.Core.Commands
{
    public interface IGenerateImdbDatabaseCommand
    {
        Task<int> Execute();
    }

    public class GenerateImdbDatabaseCommandOptions
    {
        public static string Position => "GenerateImdbDatabase";

        public int? DebugMaxImdbRowCount { get; set; }
        public string ImdbMoviesList { get; set; }
        public string ImdbAlsoKnownAsList { get; set; }
        public string ImdbRatingsList { get; set; }
        public string[] AkaFilterRegion { get; set; }
        public string[] AkaFilterLanguage { get; set; }
    }

    public class GenerateImdbDatabaseCommand : IGenerateImdbDatabaseCommand
    {
        private readonly ILogger<UpdateEpgCommand> logger;
        private readonly IServiceScopeFactory serviceScopeFactory;
        private readonly GenerateImdbDatabaseCommandOptions generateImdbDatabaseCommandOptions;
        private readonly ITheMovieDbService theMovieDbService;

        const int batchSize = 10000;

        public GenerateImdbDatabaseCommand(ILogger<UpdateEpgCommand> logger, 
            IServiceScopeFactory serviceScopeFactory,
            IOptionsSnapshot<GenerateImdbDatabaseCommandOptions> generateImdbDatabaseCommandOptions,
            ITheMovieDbService theMovieDbService)
        {
            this.logger = logger;
            this.serviceScopeFactory = serviceScopeFactory;
            this.generateImdbDatabaseCommandOptions = generateImdbDatabaseCommandOptions.Value;
            this.theMovieDbService = theMovieDbService;
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
            logger.LogInformation("Removing MovieAlternatives");
            do {
                using (var scope = serviceScopeFactory.CreateScope())
                using (var db = scope.ServiceProvider.GetRequiredService<ImdbDbContext>())
                {
                    var batch = db.MovieAlternatives.AsNoTracking().OrderBy(ma => ma.Id).Take(batchSize);
                    if (!batch.Any())
                        break;
                    db.MovieAlternatives.RemoveRange(batch);
                    await db.SaveChangesAsync();
                }
            } while (true);

            logger.LogInformation("Removing Movies");
            do {
                using (var scope = serviceScopeFactory.CreateScope())
                using (var db = scope.ServiceProvider.GetRequiredService<ImdbDbContext>())
                {
                    var batch = db.Movies.AsNoTracking().OrderBy(m => m.Id).Take(batchSize);
                    if (!batch.Any())
                        break;
                    db.Movies.RemoveRange(batch);
                    await db.SaveChangesAsync();
                }
            } while (true);
        }

        private async Task ImportImdbData_Movies()
        {
            int debugMaxImdbRowCount = generateImdbDatabaseCommandOptions.DebugMaxImdbRowCount ?? 0;

            var fileToDecompress = new FileInfo(generateImdbDatabaseCommandOptions.ImdbMoviesList);
            using (var originalFileStream = fileToDecompress.OpenRead())
            using (var decompressionStream = new GZipStream(originalFileStream, CompressionMode.Decompress))
            using (var textReader = new StreamReader(decompressionStream))
            {
                long count = 0, countAlternatives = 0, skipped = 0;
                string text = null;

                // tconst	titleType	primaryTitle	originalTitle	isAdult	startYear	endYear	runtimeMinutes	genres
                // tt0000009	movie	Miss Jerry	Miss Jerry	0	1894	\N	45	Romance
                var regex = new Regex(@"^([^\t]*)\t([^\t]*)\t([^\t]*)\t([^\t]*)\t[^\t]*\t([^\t]*)\t[^\t]*\t[^\t]*\t[^\t]*$",
                    RegexOptions.Compiled);

                var stopwatch = new Stopwatch();
                stopwatch.Start();

                // Skip header
                textReader.ReadLine();

                var FilterTypes = new string[]
                {
                    "movie",
                    "video",
                    "short",
                    "tvMovie",
                    "tvMiniSeries",
                    "tvSeries",
                };

                do {
                    using (var scope = serviceScopeFactory.CreateScope())
                    using (var db = scope.ServiceProvider.GetRequiredService<ImdbDbContext>())
                    {
                        int batchCount = 0;
                        while (batchCount < batchSize && (text = textReader.ReadLine()) != null)
                        {
                            batchCount++;
                            count++;

                            if (count % batchSize == 0)
                            {
                                logger.LogInformation(
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
                                logger.LogWarning("Unable to parse line {LineNo}: {LineText}", count, text);
                                continue;
                            }

                            // Filter on movie|video|short|tvMovie|tvMiniSeries
                            if (!FilterTypes.Contains(match.Groups[2].Value))
                            {
                                skipped++;
                                continue;
                            }

                            string movieId = match.Groups[1].Value;

                            var movie = new ImdbMovie();
                            movie.ImdbId = movieId;
                            movie.PrimaryTitle = match.Groups[3].Value;
                            string originalTitle = match.Groups[4].Value;

                            if (int.TryParse(match.Groups[5].Value, out int startYear))
                                movie.Year = startYear;

                            string primaryTitleNormalized = TitleNormalizer.NormalizeTitle(movie.PrimaryTitle);

                            countAlternatives++;
                            movie.MovieAlternatives = new List<ImdbMovieAlternative>
                            {
                                new ImdbMovieAlternative()
                                {
                                    Movie = movie,
                                    AlternativeTitle = null,
                                    Normalized = primaryTitleNormalized
                                }
                            };
                            
                            string originalTitleNormalized = TitleNormalizer.NormalizeTitle(originalTitle);
                            if (primaryTitleNormalized != originalTitleNormalized)
                            {
                                countAlternatives++;
                                movie.MovieAlternatives.Add(
                                    new ImdbMovieAlternative()
                                    {
                                        Movie = movie,
                                        AlternativeTitle = originalTitle,
                                        Normalized = originalTitleNormalized
                                    });
                            }

                            db.Movies.Add(movie);
                        }

                        await db.SaveChangesAsync();
                    }
                } while (text != null);
                
                logger.LogInformation("IMDb movies scanned: {Count}", count);
            }
        }        

        private async Task ImportImdbData_AlsoKnownAs()
        {
            int debugMaxImdbRowCount = generateImdbDatabaseCommandOptions.DebugMaxImdbRowCount ?? 0;
            string[] akaFilterRegion = generateImdbDatabaseCommandOptions.AkaFilterRegion;
            string[] akaFilterLanguage = generateImdbDatabaseCommandOptions.AkaFilterLanguage;

            var fileToDecompress = new FileInfo(generateImdbDatabaseCommandOptions.ImdbAlsoKnownAsList);
            using (var originalFileStream = fileToDecompress.OpenRead())
            using (var decompressionStream = new GZipStream(originalFileStream, CompressionMode.Decompress))
            using (var textReader = new StreamReader(decompressionStream))
            {
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
                textReader.ReadLine();

                do {

                    using (var scope = serviceScopeFactory.CreateScope())
                    using (var db = scope.ServiceProvider.GetRequiredService<ImdbDbContext>())
                    {
                        int batchCount = 0;
                        while (batchCount < batchSize && (text = textReader.ReadLine()) != null)
                        {
                            batchCount++;
                            count++;

                            if (count % batchSize == 0)
                            {
                                await db.SaveChangesAsync();
                                logger.LogInformation(
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
                                logger.LogWarning("Unable to parse line {LineNo}: {LineText}", count, text);
                                continue;
                            }

                            if (!(
                                akaFilterRegion.Contains(match.Groups[3].Value) 
                                || akaFilterLanguage.Contains(match.Groups[4].Value)
                                ))
                            {
                                skipped++;
                                continue;
                            }

                            string movieId = match.Groups[1].Value;
                            ImdbMovie movie = await db.Movies.Include(m => m.MovieAlternatives).SingleOrDefaultAsync(m => m.ImdbId == movieId);
                            if (movie == null)
                            {
                                skipped++;
                                continue;
                            }

                            string alternativeTitle = match.Groups[2].Value;

                            string normalized = TitleNormalizer.NormalizeTitle(alternativeTitle);
                            // Not in DB
                            if (movie.MovieAlternatives.Any(ma => ma.Normalized == normalized))
                            {
                                skipped++;
                                continue;
                            }

                            var movieAlternative = new ImdbMovieAlternative();
                            movieAlternative.Movie = movie;
                            movieAlternative.AlternativeTitle = alternativeTitle;
                            movieAlternative.Normalized = normalized;
                            db.MovieAlternatives.Add(movieAlternative);
                            countAlternatives++;
                        }

                        await db.SaveChangesAsync();
                    }

                } while (text != null);

                logger.LogInformation("IMDb movies scanned: {Count}", count);
            }
        }

        private async Task ImportImdbData_Ratings()
        {
            int debugMaxImdbRowCount = generateImdbDatabaseCommandOptions.DebugMaxImdbRowCount ?? 0;

            var fileToDecompress = new FileInfo(generateImdbDatabaseCommandOptions.ImdbRatingsList);
            using (var originalFileStream = fileToDecompress.OpenRead())
            using (var decompressionStream = new GZipStream(originalFileStream, CompressionMode.Decompress))
            using (var textReader = new StreamReader(decompressionStream))
            {
                long count = 0, skipped = 0;
                string text = null;

                // New  Distribution  Votes  Rank  Title
                //       0000000125  1852213   9.2  The Shawshank Redemption (1994)
                var regex = new Regex(@"^([^\t]*)\t(\d+\.\d+)\t(\d*)$",
                    RegexOptions.Compiled);

                var stopwatch = new Stopwatch();
                stopwatch.Start();

                // Skip header
                textReader.ReadLine();

                do {

                    using (var scope = serviceScopeFactory.CreateScope())
                    using (var db = scope.ServiceProvider.GetRequiredService<ImdbDbContext>())
                    {

                        int batchCount = 0;
                        while (batchCount < batchSize && (text = textReader.ReadLine()) != null)
                        {
                            batchCount++;
                            count++;

                            if (count % batchSize == 0)
                            {
                                await db.SaveChangesAsync();
                                logger.LogInformation(
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
                                logger.LogWarning("Unable to parse line {LineNo}: {LineText}", count, text);
                                continue;
                            }

                            string tconst = match.Groups[1].Value;

                            var movie = await db.Movies.SingleOrDefaultAsync(m => m.ImdbId == tconst);
                            if (movie == null)
                            {
                                // Probably a serie or ...
                                //logger.LogWarning("Unable to find movie {0}", tconst);
                                skipped++;
                                continue;
                            }

                            int votes;
                            if (int.TryParse(match.Groups[3].Value, out votes))
                                movie.Votes = votes;

                            decimal rating;
                            if (decimal.TryParse(match.Groups[2].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture.NumberFormat, out rating))
                                movie.Rating = (int)(rating * 10);
                        }

                        await db.SaveChangesAsync();
                    }

                } while (text != null);

                logger.LogInformation("IMDb ratings scanned: {Count}", count);
            }
        }

        private async Task ImportImdbData_CleanupMoviesWithoutRatings()
        {
            int year = DateTime.Now.Year - 2;

            logger.LogInformation("Removing Movies without Rating");

            int total;
            using (var scope = serviceScopeFactory.CreateScope())
            using (var db = scope.ServiceProvider.GetRequiredService<ImdbDbContext>())
            {
                total = db.Movies.Where((m) => !m.Rating.HasValue && m.Year <= year).OrderBy(m => m.Id).Count();
            }
            
            logger.LogInformation("Removing {Count} Movies without Rating", total);
            long count = 0;
            do {
                using (var scope = serviceScopeFactory.CreateScope())
                using (var db = scope.ServiceProvider.GetRequiredService<ImdbDbContext>())
                {
                    var batch = db.Movies
                        .AsNoTracking()
                        .Where((m) => !m.Rating.HasValue && m.Year <= year)
                        .OrderBy(m => m.Id)
                        .Take(batchSize)
                        .Include(m => m.MovieAlternatives);
                    count += batch.Count();
                    if (!batch.Any())
                        break;
                    // This also removes Alternatives
                    db.Movies.RemoveRange(batch);
                    await db.SaveChangesAsync();
                    logger.LogInformation("Removing {TotalCount} Movies without Rating, {PercentDone}%", total, count * 100 / total);
                }
            } while (true);
        }

        private async Task ImportImdbData_Vacuum()
        {
            using (var scope = serviceScopeFactory.CreateScope())
            using (var db = scope.ServiceProvider.GetRequiredService<ImdbDbContext>())
            {
                logger.LogInformation("Doing 'VACUUM'...");
                await db.Database.ExecuteSqlRawAsync("VACUUM;");
            }            
        }
    }
}