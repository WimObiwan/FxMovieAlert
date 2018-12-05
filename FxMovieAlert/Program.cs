using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sentry;

namespace FxMovieAlert
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateWebHostBuilder(args).Build().Run();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
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
                                ThisAssembly.Git.Commit +
                                (ThisAssembly.Git.IsDirty ? "*" : ""))
                        }
                    );
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
                    config.AddEnvironmentVariables();
                })
                .UseStartup<Startup>()
                .UseSentry("https://44d07a7cb1df484ca9a745af1ca94a2f@sentry.io/1335368");
    }
}
