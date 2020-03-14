using System;
using System.Collections.Generic;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace FxMovieAlert
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateWebHostBuilder(args).Build().Run();
        }

        public static string GetNetCoreVersion()
        {
            var assembly = typeof(System.Runtime.GCSettings).Assembly;
            var assemblyPath = assembly.CodeBase.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            int netCoreAppIndex = Array.IndexOf(assemblyPath, "Microsoft.NETCore.App");
            if (netCoreAppIndex > 0 && netCoreAppIndex < assemblyPath.Length - 2)
                return assemblyPath[netCoreAppIndex + 1];
            return null;
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
                                ThisAssembly.Git.Commit
                                //doesn't work (ThisAssembly.Git.IsDirty ? "*" : "")
                                ),
                            new KeyValuePair<string, string>("DotNetCoreVersion", 
                                GetNetCoreVersion())

                        }
                    );
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
                    config.AddEnvironmentVariables();
                })
                .UseStartup<Startup>()
                .UseSentry("https://44d07a7cb1df484ca9a745af1ca94a2f@sentry.io/1335368");
    }
}
