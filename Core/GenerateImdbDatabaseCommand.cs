using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FxMovies.FxMoviesDB;
using FxMovies.ImdbDB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FxMovies.Core
{
    public interface IGenerateImdbDatabaseCommand
    {
        Task<int> Run();
    }

    public class GenerateImdbDatabaseCommandOptions
    {
        public static string Position => "GenerateImdbDatabase";

        public int? DebugMaxImdbRowCount { get; set; }
        public string ImdbMoviesList { get; set; }
        public string ImdbAlsoKnownAsList { get; set; }
        public string ImdbRatingsList { get; set; }
    }

    public class GenerateImdbDatabaseCommand : IGenerateImdbDatabaseCommand
    {
        private readonly ILogger<UpdateEpgCommand> logger;
        private readonly IDbContextFactory<ImdbDbContext> imdbDbContextFactory;
        private readonly GenerateImdbDatabaseCommandOptions generateImdbDatabaseCommandOptions;
        private readonly ITheMovieDbService theMovieDbService;

        public GenerateImdbDatabaseCommand(ILogger<UpdateEpgCommand> logger, 
            IDbContextFactory<ImdbDbContext> imdbDbContextFactory,
            IOptionsSnapshot<GenerateImdbDatabaseCommandOptions> generateImdbDatabaseCommandOptions,
            ITheMovieDbService theMovieDbService)
        {
            this.logger = logger;
            this.imdbDbContextFactory = imdbDbContextFactory;
            this.generateImdbDatabaseCommandOptions = generateImdbDatabaseCommandOptions.Value;
            this.theMovieDbService = theMovieDbService;
        }

        public async Task<int> Run()
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
                using (var db = imdbDbContextFactory.CreateDbContext())
                {
                    var batch = db.MovieAlternatives.AsNoTracking().OrderBy(ma => ma.Id).Take(10000);
                    if (!batch.Any())
                        break;
                    db.MovieAlternatives.RemoveRange(batch);
                    await db.SaveChangesAsync();
                }
            } while (true);

            logger.LogInformation("Removing Movies");
            do {
                using (var db = imdbDbContextFactory.CreateDbContext())
                {
                    var batch = db.Movies.AsNoTracking().OrderBy(m => m.Id).Take(10000);
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
                    using (var db = imdbDbContextFactory.CreateDbContext())
                    {
                        int batchCount = 0;
                        while (batchCount < 10000 && (text = textReader.ReadLine()) != null)
                        {
                            batchCount++;
                            count++;

                            if (count % 10000 == 0)
                            {
                                logger.LogInformation(
                                    $"UpdateImdbDataWithMovies: {count} records done ({originalFileStream.Position * 100 / originalFileStream.Length}%), "
                                    + $"{countAlternatives} alternatives, {skipped} records skipped ({skipped * 100 / count}%), {stopwatch.ElapsedMilliseconds}");
                                stopwatch.Restart();                            

                                // For debugging
                                if (debugMaxImdbRowCount > 0 && count >= debugMaxImdbRowCount)
                                    break;
                            }

                            var match = regex.Match(text);
                            if (!match.Success)
                            {
                                logger.LogWarning($"Unable to parse line {count}: {text}");
                                continue;
                            }

                            // Filter on movie|video|short|tvMovie|tvMiniSeries
                            if (!FilterTypes.Contains(match.Groups[2].Value))
                            {
                                skipped++;
                                continue;
                            }

                            string movieId = match.Groups[1].Value;

                            var movie = new ImdbDB.Movie();
                            movie.ImdbId = movieId;
                            movie.PrimaryTitle = match.Groups[3].Value;
                            string originalTitle = match.Groups[4].Value;

                            if (int.TryParse(match.Groups[5].Value, out int startYear))
                                movie.Year = startYear;

                            string primaryTitleNormalized = ImdbDB.Util.NormalizeTitle(movie.PrimaryTitle);

                            countAlternatives++;
                            movie.MovieAlternatives = new List<MovieAlternative>
                            {
                                new MovieAlternative()
                                {
                                    Movie = movie,
                                    AlternativeTitle = null,
                                    Normalized = primaryTitleNormalized
                                }
                            };
                            
                            string originalTitleNormalized = ImdbDB.Util.NormalizeTitle(originalTitle);
                            if (primaryTitleNormalized != originalTitleNormalized)
                            {
                                countAlternatives++;
                                movie.MovieAlternatives.Add(
                                    new MovieAlternative()
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
                
                logger.LogInformation($"IMDb movies scanned: {count}");
            }
        }        

        private async Task ImportImdbData_AlsoKnownAs()
        {
            int debugMaxImdbRowCount = generateImdbDatabaseCommandOptions.DebugMaxImdbRowCount ?? 0;

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

                var FilterRegion = new string[]
                {
                    "\\N",
                    "BE",
                    "NL",
                    "US",
                    "GB"
                };
                // var FilterLanguage = new string[]
                // {
                //     "en",
                //     "nl",
                // };

                do {

                    using (var db = imdbDbContextFactory.CreateDbContext())
                    {
                        int batchCount = 0;
                        while (batchCount < 10000 && (text = textReader.ReadLine()) != null)
                        {
                            batchCount++;
                            count++;

                            if (count % 10000 == 0)
                            {
                                await db.SaveChangesAsync();
                                logger.LogInformation(
                                    $"UpdateImdbDataWithAkas: {count} records done ({originalFileStream.Position * 100 / originalFileStream.Length}%), "
                                    + $"{countAlternatives} alternatives, {skipped} records skipped ({skipped * 100 / count}%), {stopwatch.ElapsedMilliseconds}");
                                stopwatch.Restart();                            

                                // For debugging
                                if (debugMaxImdbRowCount > 0 && count >= debugMaxImdbRowCount)
                                    break;
                            }

                            var match = regex.Match(text);
                            if (!match.Success)
                            {
                                logger.LogWarning($"Unable to parse line {count}: {text}");
                                continue;
                            }

                            if (!(FilterRegion.Contains(match.Groups[3].Value) 
                                //|| FilterLanguage.Contains(match.Groups[4].Value)
                                ))
                            {
                                skipped++;
                                continue;
                            }

                            string movieId = match.Groups[1].Value;
                            ImdbDB.Movie movie = await db.Movies.Include(m => m.MovieAlternatives).SingleOrDefaultAsync(m => m.ImdbId == movieId);
                            if (movie == null)
                            {
                                skipped++;
                                continue;
                            }

                            string alternativeTitle = match.Groups[2].Value;

                            string normalized = ImdbDB.Util.NormalizeTitle(alternativeTitle);
                            // Not in DB
                            if (movie.MovieAlternatives.Any(ma => ma.Normalized == normalized))
                            {
                                skipped++;
                                continue;
                            }

                            var movieAlternative = new MovieAlternative();
                            movieAlternative.Movie = movie;
                            movieAlternative.AlternativeTitle = alternativeTitle;
                            movieAlternative.Normalized = normalized;
                            db.MovieAlternatives.Add(movieAlternative);
                            countAlternatives++;
                        }

                        await db.SaveChangesAsync();
                    }

                } while (text != null);

                logger.LogInformation($"IMDb movies scanned: {count}");
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

                    using (var db = imdbDbContextFactory.CreateDbContext())
                    {

                        int batchCount = 0;
                        while (batchCount < 10000 && (text = textReader.ReadLine()) != null)
                        {
                            batchCount++;
                            count++;

                            if (count % 10000 == 0)
                            {
                                await db.SaveChangesAsync();
                                logger.LogInformation(
                                    $"UpdateImdbDataWithRatings: {count} records done ({originalFileStream.Position * 100 / originalFileStream.Length}%), "
                                    + $"{skipped} records skipped, {stopwatch.ElapsedMilliseconds}");
                                stopwatch.Restart();                            

                                // For debugging
                                if (debugMaxImdbRowCount > 0 && count >= debugMaxImdbRowCount)
                                    break;

                            }

                            var match = regex.Match(text);
                            if (!match.Success)
                            {
                                logger.LogWarning($"Unable to parse line {count}: {text}");
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

                logger.LogInformation($"IMDb ratings scanned: {count}");
            }
        }

        private async Task ImportImdbData_CleanupMoviesWithoutRatings()
        {
            int year = DateTime.Now.Year - 2;

            logger.LogInformation($"Removing Movies without Rating");

            int total;
            using (var db = imdbDbContextFactory.CreateDbContext())
            {
                total = db.Movies.Where((m) => !m.Rating.HasValue && m.Year <= year).OrderBy(m => m.Id).Count();
            }
            
            logger.LogInformation($"Removing {total} Movies without Rating");
            long count = 0;
            do {
                using (var db = imdbDbContextFactory.CreateDbContext())
                {
                    var batch = db.Movies
                        .AsNoTracking()
                        .Where((m) => !m.Rating.HasValue && m.Year <= year)
                        .OrderBy(m => m.Id)
                        .Take(10000)
                        .Include(m => m.MovieAlternatives);
                    count += batch.Count();
                    if (!batch.Any())
                        break;
                    // This also removes Alternatives
                    db.Movies.RemoveRange(batch);
                    await db.SaveChangesAsync();
                    logger.LogInformation($"Removing {total} Movies without Rating, {count * 100 / total}%");
                }
            } while (true);
        }

        private async Task ImportImdbData_Vacuum()
        {
            using (var db = imdbDbContextFactory.CreateDbContext())
            {
                logger.LogInformation("Doing 'VACUUM'...");
                await db.Database.ExecuteSqlRawAsync("VACUUM;");
            }            
        }
    }
}