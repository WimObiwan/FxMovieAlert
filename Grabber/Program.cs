using System;
using System.Threading.Tasks;
using CommandLine;
using FxMovies.Core;
using FxMovies.Core.Commands;
using FxMovies.Core.Queries;
using FxMovies.FxMoviesDB;
using FxMovies.ImdbDB;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sentry;
using Serilog;
using Serilog.Events;

namespace FxMovies.Grabber;

internal class Program
{
    public class TemporaryFxMoviesDbContextFactory : IDesignTimeDbContextFactory<FxMoviesDbContext>
    {
        public FxMoviesDbContext CreateDbContext(string[] args)
        {
            var builder = new DbContextOptionsBuilder<FxMoviesDbContext>();

            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", false, false)
                .AddEnvironmentVariables()
                .Build();

            // Get the connection string
            var connectionString = configuration.GetConnectionString("FxMoviesDb");

            builder.UseSqlite(connectionString);
            return new FxMoviesDbContext(builder.Options);
        }
    }

    public class TemporaryImdbDbContextFactory : IDesignTimeDbContextFactory<ImdbDbContext>
    {
        public ImdbDbContext CreateDbContext(string[] args)
        {
            var builder = new DbContextOptionsBuilder<ImdbDbContext>();

            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", false, false)
                .AddEnvironmentVariables()
                .Build();

            // Get the connection string
            var connectionString = configuration.GetConnectionString("ImdbDb");

            builder.UseSqlite(connectionString);
            return new ImdbDbContext(builder.Options);
        }
    }

    [Verb("Help", HelpText = "Shows help about Grabber.")]
    private class HelpOptions
    {
    }

    [Verb("GenerateImdbDatabase", HelpText = "Generate IMDb database.")]
    private class GenerateImdbDatabaseOptions
    {
    }

    [Verb("UpdateImdbUserData", HelpText = "Update IMDb user ratings & watchlist.")]
    private class UpdateImdbUserDataOptions
    {
        [Option("imdbuserid", Required = true, HelpText = "IMDb user ID")]
        public string ImdbUserId { get; set; }

        [Option("updateAllRatings", HelpText = "Update all ratings instead of the last 100")]
        public bool UpdateAllRatings { get; set; }
    }

    [Verb("UpdateAllImdbUsersData", HelpText = "Update all IMDb users ratings & watchlist.")]
    private class UpdateAllImdbUsersDataOptions
    {
    }

    [Verb("AutoUpdateImdbUserData", HelpText = "Auto update IMDb users ratings & watchlist.")]
    private class AutoUpdateImdbUserDataOptions
    {
    }

    [Verb("UpdateEPG", HelpText = "Update EPG.")]
    private class UpdateEpgOptions
    {
    }

    [Verb("ListManualMatches", HelpText = "List manual matches.")]
    private class ListManualMatchesOptions
    {
    }

    [Verb("Stats", HelpText = "Show statistics.")]
    private class StatsOptions
    {
    }

    // [Verb("TwitterBot", HelpText = "Twitter Bot.")]
    // class TwitterBotOptions
    // {
    // }

    // [Verb("Manual", HelpText = "Manual.")]
    // class ManualOptions
    // {
    //     [Option("movieeventid", Required = true, HelpText = "Movie event ID")]
    //     public int MovieEventId { get; set; }
    //     [Option("imdbid", Required = true, HelpText = "IMDb ID")]
    //     public string ImdbId { get; set; }
    // }

    [Verb("TestImdbMatching", HelpText = "Test IMDb matching.")]
    private class TestImdbMatchingOptions
    {
        [Option("title", Required = true, HelpText = "Movie title")]
        public string Title { get; set; }

        [Option("year", Required = false, HelpText = "Movie release year")]
        public int? Year { get; set; }
    }

    [Verb("TestSentry", HelpText = "Test Sentry.")]
    private class TestSentryOptions
    {
    }

    private static async Task<int> Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateLogger();

        try
        {
            using (var host = CreateHostBuilder(args).Build())
            {
                var versionInfo = host.Services.GetRequiredService<IVersionInfo>();
                var logger = host.Services.GetRequiredService<ILogger<Program>>();
                logger.LogInformation("Version {Version}, running on {DotNetCoreVersion}", versionInfo.Version,
                    versionInfo.DotNetCoreVersion);

                var serviceProvider = host.Services.GetRequiredService<IServiceProvider>();
                using (var scope = serviceProvider.CreateScope())
                {
                    var fxMoviesDbContext = scope.ServiceProvider.GetRequiredService<FxMoviesDbContext>();
                    //await fxMoviesDbContext.Database.EnsureCreatedAsync();
                    await fxMoviesDbContext.Database.MigrateAsync();
                }

                using (var scope = serviceProvider.CreateScope())
                {
                    var imdbDbContext = scope.ServiceProvider.GetRequiredService<ImdbDbContext>();
                    await imdbDbContext.Database.EnsureCreatedAsync();
                    //await imdbDbContext.Database.MigrateAsync();
                }

                using (SentrySdk.Init(o =>
                {
                    o.Dsn = "https://3181503fa0264cdb827506614c8973f2@o210563.ingest.sentry.io/1335361";
                    // When configuring for the first time, to see what the SDK is doing:
                    //o.Debug = true;
                    // Set traces_sample_rate to 1.0 to capture 100% of transactions for performance monitoring.
                    // We recommend adjusting this value in production.
                    o.TracesSampleRate = 1.0;
                }))
                {
                    try
                    {
                        return await Parser.Default
                            .ParseArguments<
                                HelpOptions,
                                GenerateImdbDatabaseOptions,
                                UpdateImdbUserDataOptions,
                                UpdateAllImdbUsersDataOptions,
                                AutoUpdateImdbUserDataOptions,
                                UpdateEpgOptions,
                                ListManualMatchesOptions,
                                StatsOptions,
                                // TwitterBotOptions,
                                // ManualOptions,
                                TestImdbMatchingOptions,
                                TestSentryOptions
                            >(args)
                            .MapResult(
                                (HelpOptions o) => Run(o),
                                (GenerateImdbDatabaseOptions o) =>
                                    host.Services.GetRequiredService<IGenerateImdbDatabaseCommand>().Execute(),
                                (UpdateImdbUserDataOptions o) =>
                                    host.Services.GetRequiredService<IUpdateImdbUserDataCommand>()
                                        .Execute(o.ImdbUserId, o.UpdateAllRatings),
                                (UpdateAllImdbUsersDataOptions o) => host.Services
                                    .GetRequiredService<IUpdateAllImdbUsersDataCommand>().Execute(),
                                (AutoUpdateImdbUserDataOptions o) => host.Services
                                    .GetRequiredService<IAutoUpdateImdbUserDataCommand>().Execute(),
                                (UpdateEpgOptions o) => host.Services.GetRequiredService<IUpdateEpgCommand>().Execute(),
                                (ListManualMatchesOptions o) => Run(host, o),
                                (StatsOptions o) => Run(host, o),
                                // (TwitterBotOptions o) => Run(o),
                                // (ManualOptions o) => Run(o),
                                (TestImdbMatchingOptions o) => Run(host, o),
                                (TestSentryOptions o) => Run(o),
                                errs => Task.FromResult(1));
                    }
                    catch (Exception x)
                    {
                        SentrySdk.CaptureException(x);
                        throw;
                    }
                }
            }
        }
        catch (Exception x)
        {
            Log.Fatal(x, "Host terminated unexpectedly");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureAppConfiguration((hostingContext, config) =>
            {
                config
                    //.SetBasePath(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location))
                    .AddJsonFile("appsettings.json", false, false)
                    .AddJsonFile($"appsettings.{hostingContext.HostingEnvironment.EnvironmentName}.json", true, false)
                    .AddJsonFile("appsettings.Local.json", true, false)
                    .AddEnvironmentVariables();
            })
            .UseStartup<Startup>();
    }

    private static Task<int> Run(HelpOptions options)
    {
        return Task.FromResult(0);
    }

    // private static int Run(TwitterBotOptions options)
    // {
    //     TwitterBot();
    //     return 0;
    // }

    // private static int Run(ManualOptions options)
    // {
    //     UpdateEpgDataWithImdbManual(options.MovieEventId, options.ImdbId);
    //     return 0;
    // }

    private static async Task<int> Run(IHost host, ListManualMatchesOptions o)
    {
        var logger = host.Services.GetRequiredService<ILogger<Program>>();

        var query = host.Services.GetRequiredService<IListManualMatchesQuery>();
        foreach (var manualMatch in await query.Execute())
        {
            logger.LogInformation("{Id} {Title} {ImdbId} {AddedDateTime}",
                manualMatch.Id, manualMatch.Title, manualMatch.Movie?.ImdbId, manualMatch.AddedDateTime);
            Console.WriteLine("{0} {1} {2} {3}",
                manualMatch.Id, manualMatch.Title, manualMatch.Movie?.ImdbId, manualMatch.AddedDateTime);
        }

        return 0;
    }

    private static async Task<int> Run(IHost host, StatsOptions o)
    {
        var logger = host.Services.GetRequiredService<ILogger<Program>>();

        var query = host.Services.GetRequiredService<IStatsQuery>();
        var statsResult = await query.Execute();
        foreach (var user in statsResult.Users)
        {
            logger.LogInformation("{UserId} {ImdbUserId} {LastUsageTime} {Usages} {RatingCount} {WatchListItemsCount}",
                user.UserId, user.ImdbUserId, user.LastUsageTime, user.Usages, user.RatingCount,
                user.WatchListItemsCount);
            Console.WriteLine("{0} {1} {2} {3} {4} {5}",
                user.UserId, user.ImdbUserId, user.LastUsageTime, user.Usages, user.RatingCount,
                user.WatchListItemsCount);
        }

        return 0;
    }

    private static async Task<int> Run(IHost host, TestImdbMatchingOptions o)
    {
        var movieTitle = o.Title;
        var movieReleaseYear = o.Year;
        var logger = host.Services.GetRequiredService<ILogger<Program>>();

        var query = host.Services.GetRequiredService<IImdbMatchingQuery>();
        var result = await query.Execute(movieTitle, movieReleaseYear);
        var imdbMovie = result.ImdbMovie;
        if (imdbMovie != null)
        {
            Console.WriteLine(
                $"Movie '{movieTitle}' ({movieReleaseYear}) found (#{result.HuntNo}): {imdbMovie.ImdbId} - '{imdbMovie.PrimaryTitle}' ({imdbMovie.Year})");
            return 0;
        }
        else
        {
            logger.LogError($"Movie '{movieTitle}' ({movieReleaseYear}) not found");
            return 1;
        }
    }

    private static Task<int> Run(TestSentryOptions options)
    {
        throw new Exception("Test Sentry");
    }
}