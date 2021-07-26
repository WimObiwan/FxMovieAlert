using System;
using System.Collections.Generic;
using FxMovies.FxMoviesDB;
using FxMovies.ImdbDB;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using CommandLine;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using FxMovies.Core;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace FxMovies.Grabber
{
    class Program
    {
        public class TemporaryFxMoviesDbContextFactory : IDesignTimeDbContextFactory<FxMoviesDbContext>
        {
            public FxMoviesDbContext CreateDbContext(string[] args)
            {
                var builder = new DbContextOptionsBuilder<FxMoviesDbContext>();

                var configuration = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                    .AddEnvironmentVariables()
                    .Build();

                // Get the connection string
                string connectionString = configuration.GetConnectionString("FxMoviesDb");

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
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                    .AddEnvironmentVariables()
                    .Build();

                // Get the connection string
                string connectionString = configuration.GetConnectionString("ImdbDb");

                builder.UseSqlite(connectionString); 
                return new ImdbDbContext(builder.Options); 
            }
        }

        [Verb("Help", HelpText = "Shows help about Grabber.")]
        class HelpOptions
        {
        }

        [Verb("GenerateImdbDatabase", HelpText = "Generate IMDb database.")]
        class GenerateImdbDatabaseOptions
        {
        }

        [Verb("UpdateImdbUserData", HelpText = "Update IMDb user ratings & watchlist.")]
        class UpdateImdbUserDataOptions
        {
            [Option("imdbuserid", Required = true, HelpText = "IMDb user ID")]
            public string ImdbUserId { get; set; }
            [Option("updateAllRatings", HelpText = "Update all ratings instead of the last 100")]
            public bool UpdateAllRatings { get; set; }
            
        }

        [Verb("UpdateAllImdbUsersData", HelpText = "Update all IMDb users ratings & watchlist.")]
        class UpdateAllImdbUsersDataOptions
        {
        }

        [Verb("AutoUpdateImdbUserData", HelpText = "Auto update IMDb users ratings & watchlist.")]
        class AutoUpdateImdbUserDataOptions
        {
        }

        [Verb("UpdateEPG", HelpText = "Update EPG.")]
        class UpdateEpgOptions
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
        class TestImdbMatchingOptions
        {
            [Option("title", Required = true, HelpText = "Movie title")]
            public string Title { get; set; }
            [Option("year", Required = false, HelpText = "Movie release year")]
            public int Year { get; set; }
        }

        [Verb("TestSentry", HelpText = "Test Sentry.")]
        class TestSentryOptions
        {
        }

        static async Task<int> Main(string[] args)
        {
            using (var host = CreateHostBuilder(args).Build())
            {
                var config = host.Services.GetRequiredService<IConfiguration>();
                var version = config.GetValue<string>("Version");
                var logger = host.Services.GetRequiredService<ILogger<Program>>();
                logger.LogInformation($"Version {version}");

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

                using (Sentry.SentrySdk.Init("https://3181503fa0264cdb827506614c8973f2@sentry.io/1335361"))
                {
                    return await Parser.Default
                        .ParseArguments<
                            HelpOptions,
                            GenerateImdbDatabaseOptions,
                            UpdateImdbUserDataOptions,
                            UpdateAllImdbUsersDataOptions,
                            AutoUpdateImdbUserDataOptions,
                            UpdateEpgOptions,
                            // TwitterBotOptions,
                            // ManualOptions,
                            TestImdbMatchingOptions,
                            TestSentryOptions
                            >(args)
                        .MapResult(
                            (HelpOptions o) => Run(o),
                            (GenerateImdbDatabaseOptions o) => host.Services.GetRequiredService<IGenerateImdbDatabaseCommand>().Run(),
                            (UpdateImdbUserDataOptions o) => host.Services.GetRequiredService<IUpdateImdbUserDataCommand>().Run(o.ImdbUserId, o.UpdateAllRatings),
                            (UpdateAllImdbUsersDataOptions o) => host.Services.GetRequiredService<IUpdateAllImdbUsersDataCommand>().Run(),
                            (AutoUpdateImdbUserDataOptions o) => host.Services.GetRequiredService<IAutoUpdateImdbUserDataCommand>().Run(),
                            (UpdateEpgOptions o) => host.Services.GetRequiredService<IUpdateEpgCommand>().Run(),
                            // (TwitterBotOptions o) => Run(o),
                            // (ManualOptions o) => Run(o),
                            (TestImdbMatchingOptions o) => host.Services.GetRequiredService<IImdbMatchingQuery>().RunTest(o.Title, o.Year),
                            (TestSentryOptions o) => Run(o),
                            errs => Task.FromResult(1));
                }
            }
        }

        static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.AddInMemoryCollection(
                        new List<KeyValuePair<string, string>>()
                        {
                            new KeyValuePair<string, string>("Version", 
                                ThisAssembly.Git.SemVer.Major + "."
                                + ThisAssembly.Git.SemVer.Minor + "."
                                + ThisAssembly.Git.Commits + "-"
                                + ThisAssembly.Git.Branch + "+"
                                + ThisAssembly.Git.Commit
                                //+ (ThisAssembly.Git.IsDirty ? "*" : "")
                                ),
                            new KeyValuePair<string, string>("DotNetCoreVersion", 
                                System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription)

                        }
                    )
                    //.SetBasePath(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location))
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                    .AddJsonFile($"appsettings.{hostingContext.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: false)
                    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false)
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

        private static Task<int> Run(TestSentryOptions options)
        {
            throw new Exception("Test Sentry");
        }

    }
}

