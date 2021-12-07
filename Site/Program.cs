using System;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;

namespace FxMovies.Site;

public static class Program
{
    public static int Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateLogger();

        try
        {
            Log.Information("Starting web host");
            CreateHostBuilder(args).Build().Run();
            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Host terminated unexpectedly");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static IWebHostBuilder CreateHostBuilder(string[] args)
    {
        return WebHost.CreateDefaultBuilder(args)
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