using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.RateLimiting;
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
using Microsoft.Extensions.Logging;
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

        // Add Aspire service defaults
        builder.AddServiceDefaults();

        builder.Configuration.AddJsonFile("appsettings.Local.json", true, false);

        ConfigureHost(builder.Host);
        ConfigureWebHost(builder.WebHost);
        ConfigureServices(builder.Services, builder.Environment, builder.Configuration);

        var webApplication = builder.Build();

        // Map Aspire default endpoints
        webApplication.MapDefaultEndpoints();

        ConfigureMiddleware(webApplication, builder.Configuration);

        return webApplication;
    }

    private static void ConfigureHost(IHostBuilder hostBuilder)
    {
        hostBuilder.UseSerilog((context, services, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
        );
    }

    private static void ConfigureWebHost(IWebHostBuilder webHostBuilder)
    {
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
        services.Configure<RateLimitOptions>(configuration.GetSection(RateLimitOptions.Position));

        var rateLimitOptions = configuration.GetSection(RateLimitOptions.Position).Get<RateLimitOptions>();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            
            options.OnRejected = async (context, cancellationToken) =>
            {
                var logger = context.HttpContext.RequestServices.GetService<ILoggerFactory>()?.CreateLogger("RateLimiter");
                var ipAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var path = context.HttpContext.Request.Path;
                logger?.LogWarning("Rate limit exceeded for IP {IpAddress} on path {Path}", ipAddress, path);
                
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.HttpContext.Response.WriteAsync("Too many requests. Please try again later.", cancellationToken);
            };
            
            options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                CreateRateLimitPartition(rateLimitOptions, rateLimitOptions?.PermitLimit ?? 5, 
                    TimeSpan.FromSeconds(rateLimitOptions?.Window ?? 1),
                    rateLimitOptions?.QueueLimit ?? 0),
                CreateRateLimitPartition(rateLimitOptions, rateLimitOptions?.PermitLimitPerMinute ?? 15,
                    TimeSpan.FromMinutes(rateLimitOptions?.WindowMinute ?? 1),
                    0),
                CreateRateLimitPartition(rateLimitOptions, rateLimitOptions?.PermitLimitPerHour ?? 100,
                    TimeSpan.FromHours(rateLimitOptions?.WindowHour ?? 1),
                    0)
            );
        });

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

        app.UseRateLimiter();

        app.UseAuthentication();

        app.UseFxMoviesHealthChecks();

        app.UseRouting();

        app.UseSentryTracing();

        app.UseAuthorization();

        app.MapRazorPages();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        app.MapGet("/Streaming", ctx =>
        {
            ctx.Response.Redirect("/FreeStreaming" + ctx.Request.QueryString, true);
            return Task.CompletedTask;
        });
    }

    private static void SetCacheControlHeader(StaticFileResponseContext ctx)
    {
        const int durationInSeconds = 60 * 60 * 24;
        ctx.Context.Response.Headers[HeaderNames.CacheControl] = "public,max-age=" + durationInSeconds;
    }

    private static bool IsStaticFile(HttpContext httpContext)
    {
        var path = httpContext.Request.Path.ToString();
        return path.StartsWith("/wwwroot/") || 
               path.StartsWith("/images/") || 
               path.StartsWith("/css/") || 
               path.StartsWith("/js/") || 
               path.StartsWith("/lib/") ||
               path.EndsWith(".css") ||
               path.EndsWith(".js") ||
               path.EndsWith(".png") ||
               path.EndsWith(".jpg") ||
               path.EndsWith(".jpeg") ||
               path.EndsWith(".gif") ||
               path.EndsWith(".ico") ||
               path.EndsWith(".svg") ||
               path.EndsWith(".woff") ||
               path.EndsWith(".woff2") ||
               path.EndsWith(".ttf") ||
               path.EndsWith(".eot");
    }

    private static bool ShouldApplyRateLimit(HttpContext httpContext, RateLimitOptions rateLimitOptions)
    {
        if (httpContext.Request.Method != HttpMethods.Get || IsStaticFile(httpContext))
        {
            return false;
        }

        var remoteIp = httpContext.Connection.RemoteIpAddress;
        if (remoteIp == null)
        {
            return true;
        }

        // Check if IP is in whitelist
        if (rateLimitOptions?.WhitelistedIPs != null)
        {
            foreach (var whitelistedEntry in rateLimitOptions.WhitelistedIPs)
            {
                if (string.IsNullOrWhiteSpace(whitelistedEntry))
                {
                    continue;
                }

                try
                {
                    // Try to parse as CIDR notation
                    if (IPNetwork2.TryParse(whitelistedEntry, out var network))
                    {
                        if (network.Contains(remoteIp))
                        {
                            return false; // IP is whitelisted
                        }
                    }
                }
                catch
                {
                    // Invalid CIDR notation, skip
                }
            }
        }

        return true;
    }

    private static PartitionedRateLimiter<HttpContext> CreateRateLimitPartition(
        RateLimitOptions rateLimitOptions, int permitLimit, TimeSpan window, int queueLimit)
    {
        return PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        {
            if (!ShouldApplyRateLimit(httpContext, rateLimitOptions))
            {
                return RateLimitPartition.GetNoLimiter<string>("no-limit");
            }
            
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter(ipAddress, _ =>
                new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = window,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = queueLimit
                });
        });
    }
}