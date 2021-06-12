using System;
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
                    );
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
                    config.AddJsonFile($"appsettings.{hostingContext.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: false);
                    config.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);
                    config.AddEnvironmentVariables();
                })
                .UseStartup<Startup>()
                //.UseSentry("https://44d07a7cb1df484ca9a745af1ca94a2f@sentry.io/1335368")
                ;
    }
}
