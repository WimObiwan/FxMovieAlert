using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml;
using FxMovies.FxMoviesDB;
using FxMovies.ImdbDB;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using CommandLine;
using CommandLine.Text;
using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using FxMovies.Core;
using System.Threading.Tasks;
using System.Reflection;

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

        [Verb("UpdateImdbUserRatings", HelpText = "Update IMDb user ratings.")]
        class UpdateImdbUserRatingsOptions
        {
            [Option("userid", Required = true, HelpText = "User ID")]
            public string UserId { get; set; }
            [Option("imdbuserid", Required = true, HelpText = "IMDb user ID")]
            public string ImdbUserId { get; set; }
        }

        [Verb("AutoUpdateImdbUserRatings", HelpText = "Auto update IMDb user ratings.")]
        class AutoUpdateImdbUserRatingsOptions
        {
        }

        [Verb("UpdateEPG", HelpText = "Update EPG.")]
        class UpdateEpgOptions
        {
        }

        // [Verb("UpdateVod", HelpText = "Update VOD.")]
        // class UpdateVodOptions
        // {
        // }

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

        [Verb("TestSentry", HelpText = "Test Sentry.")]
        class TestSentryOptions
        {
        }

        static async Task<int> Main(string[] args)
        {
            using (var host = CreateHostBuilder(args).Build())
            {
                Console.WriteLine("Version " +
                    ThisAssembly.Git.SemVer.Major + "." +
                    ThisAssembly.Git.SemVer.Minor + "." +
                    ThisAssembly.Git.Commits + "-" +
                    ThisAssembly.Git.Branch + "+" +
                    ThisAssembly.Git.Commit
                    //doesn't work (ThisAssembly.Git.IsDirty ? "*" : "")
                    );

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
                            // UpdateImdbUserRatingsOptions,
                            // AutoUpdateImdbUserRatingsOptions,
                            UpdateEpgOptions,
                            // UpdateVodOptions,
                            // TwitterBotOptions,
                            // ManualOptions,
                            TestSentryOptions
                            >(args)
                        .MapResult(
                            (HelpOptions o) => Run(o),
                            (GenerateImdbDatabaseOptions o) => host.Services.GetRequiredService<IGenerateImdbDatabaseCommand>().Run(),
                            // (UpdateImdbUserRatingsOptions o) => Run(o),
                            // (AutoUpdateImdbUserRatingsOptions o) => Run(o),
                            (UpdateEpgOptions o) => host.Services.GetRequiredService<IUpdateEpgCommand>().Run(),
                            // (UpdateVodOptions o) => Run(o),
                            // (TwitterBotOptions o) => Run(o),
                            // (ManualOptions o) => Run(o),
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
                                ThisAssembly.Git.SemVer.Major + "." +
                                ThisAssembly.Git.SemVer.Minor + "." +
                                ThisAssembly.Git.Commits + "-" +
                                ThisAssembly.Git.Branch + "+" +
                                ThisAssembly.Git.Commit
                                //doesn't work (ThisAssembly.Git.IsDirty ? "*" : "")
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

        // private static int Run(UpdateImdbUserRatingsOptions options)
        // {
        //     UpdateImdbUserRatings(options.UserId, options.ImdbUserId);
        //     return 0;
        // }

        // private static int Run(AutoUpdateImdbUserRatingsOptions options)
        // {
        //     AutoUpdateImdbUserRatings();
        //     return 0;
        // }

        // private static int Run(UpdateVodOptions options)
        // {
        //     UpdateDatabaseVod_YeloPlay();
        //     UpdateVodDataWithImdb();
        //     return 0;
        // }

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

