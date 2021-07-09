using System.Collections.Generic;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace FxMovieAlert
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IWebHostBuilder CreateHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
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
                .UseSentry(o =>
                {
                    o.Dsn = "https://44d07a7cb1df484ca9a745af1ca94a2f@o210563.ingest.sentry.io/1335368";
                    // When configuring for the first time, to see what the SDK is doing:
                    // o.Debug = true;
                    // Set traces_sample_rate to 1.0 to capture 100% of transactions for performance monitoring.
                    // We recommend adjusting this value in production.
                    o.TracesSampleRate = 1.0;
                })
                .UseStartup<Startup>();
    }
}
