using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using FxMovies.Core;
using FxMovies.ImdbDB;
using FxMovies.MoviesDB;
using FxMovies.Site.HealthChecks;
using FxMovies.Site.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Net.Http.Headers;
using Serilog;
using Serilog.Events;
using SameSiteMode = Microsoft.AspNetCore.Http.SameSiteMode;

namespace FxMovies.Site;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateBootstrapLogger();

        try
        {
            Log.Information("Starting web host");
            await CreateWebApplication(args).RunAsync();
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

    private static WebApplication CreateWebApplication(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Configuration.AddJsonFile("appsettings.Local.json", true, false);

        ConfigureWebHost(builder.WebHost);
        ConfigureServices(builder.Services, builder.Environment, builder.Configuration);

        var webApplication = builder.Build();

        ConfigureMiddleware(webApplication, builder.Configuration);

        return webApplication;
    }

    private static void ConfigureWebHost(IWebHostBuilder webHostBuilder)
    {
        webHostBuilder.UseSerilog();
        webHostBuilder.UseSentry();
    }

    private static void ConfigureServices(IServiceCollection services, IWebHostEnvironment environment,
        IConfiguration configuration)
    {
        var pathToCryptoKeys = configuration["DataProtection:PathToCryptoKeys"];
        if (string.IsNullOrEmpty(pathToCryptoKeys))
            pathToCryptoKeys = Path.Join(environment.ContentRootPath, "dp_keys");
        Directory.CreateDirectory(pathToCryptoKeys);
        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(pathToCryptoKeys));

        // Add authentication services
        services.Configure<CookiePolicyOptions>(options =>
        {
            // This lambda determines whether user consent for non-essential cookies is needed for a given request.
            options.CheckConsentNeeded = _ => true;
            options.Secure = CookieSecurePolicy.Always;
            options.MinimumSameSitePolicy = SameSiteMode.None;
        });

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders =
                ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        });

        // Add framework services.
        services.AddRazorPages();

        services.AddWebOptimizer();

        services.AddDbContext<MoviesDbContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("FxMoviesDB")));

        services.AddDbContext<ImdbDbContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("ImdbDb")));

        services.Configure<SiteOptions>(configuration.GetSection(SiteOptions.Position));

        services.AddFxMoviesAuthentication(configuration.GetSection(Auth0Options.Position).Get<Auth0Options>());
        services.AddFxMoviesHealthChecks(configuration);
        services.AddFxMoviesCore(configuration, typeof(Program).Assembly);
    }

    private static void ConfigureMiddleware(WebApplication app, IConfiguration configuration)
    {
        // https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-2.1
        app.UseForwardedHeaders();

        var supportedCultures = new[]
        {
            new CultureInfo("nl-BE"),
            new CultureInfo("nl")
        };

        app.UseRequestLocalization(new RequestLocalizationOptions
        {
            DefaultRequestCulture = new RequestCulture(new CultureInfo("nl-BE")),
            // Formatting numbers, dates, etc.
            SupportedCultures = supportedCultures,
            // UI strings that we have localized.
            SupportedUICultures = supportedCultures
        });

        if (app.Environment.IsDevelopment())
            app.UseDeveloperExceptionPage();
        else
            app.UseExceptionHandler("/Error");
        //app.UseHsts(); // handles by nginx

        // //forward headers from the LB
        // var forwardOpts = new ForwardedHeadersOptions
        // {
        //     ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor
        // };
        // //TODO: Set this up to only accept the forwarded headers from the load balancer
        // forwardOpts.KnownNetworks.Clear();
        // forwardOpts.KnownProxies.Clear();
        // app.UseForwardedHeaders(forwardOpts);

        //app.UseHttpsRedirection(); // handles by nginx
        app.UseWebOptimizer();

        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = SetCacheControlHeader
        });

        var cachedImagePath = configuration["ImageBasePath"];
        if (string.IsNullOrEmpty(cachedImagePath))
            cachedImagePath = Path.Join(app.Environment.ContentRootPath, "wwwroot", "images", "cache");
        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = SetCacheControlHeader,
            RequestPath = "/images/cache",
            FileProvider = new PhysicalFileProvider(cachedImagePath)
        });

        app.UseCookiePolicy();

        app.UseAuthentication();

        app.UseFxMoviesHealthChecks();

        app.UseRouting();

        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapRazorPages();
            endpoints.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
            endpoints.MapGet("/Streaming", ctx =>
            {
                ctx.Response.Redirect("/FreeStreaming" + ctx.Request.QueryString, true);
                return Task.CompletedTask;
            });
        });
    }

    private static void SetCacheControlHeader(StaticFileResponseContext ctx)
    {
        const int durationInSeconds = 60 * 60 * 24;
        ctx.Context.Response.Headers[HeaderNames.CacheControl] = "public,max-age=" + durationInSeconds;
    }
}