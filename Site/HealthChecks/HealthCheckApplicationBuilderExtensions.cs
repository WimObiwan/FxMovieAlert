using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FxMovies.Core.Entities;
using FxMovies.Site.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace FxMovies.Site.HealthChecks;

internal static class HealthCheckApplicationBuilderExtensions
{
    public static IServiceCollection AddFxMoviesHealthChecks(this IServiceCollection services,
        IConfiguration configuration)
    {
        IConfiguration section = configuration.GetSection(HealthCheckOptions.Position);
        services.Configure<HealthCheckOptions>(section);
        var healthCheckOptions = section.Get<HealthCheckOptions>();

        if (healthCheckOptions.Uri != null)
            services.AddHealthChecks()
                .AddSqlite(
                    configuration.GetConnectionString("FxMoviesDB"),
                    name: "sqlite-FxMoviesDB")
                // .AddSqlite(
                //     sqliteConnectionString: configuration.GetConnectionString("FxMoviesHistoryDb"), 
                //     name: "sqlite-FxMoviesHistoryDb")
                .AddSqlite(
                    configuration.GetConnectionString("ImdbDb"),
                    name: "sqlite-ImdbDb")
                .AddIdentityServer(
                    new Uri($"https://{configuration["Auth0:Domain"]}"),
                    "idsvr-Auth0")
                .AddMovieDbDataCheck("FxMoviesDB-Broadcasts-data", MovieEvent.FeedType.Broadcast)
                .AddMovieDbDataCheck("FxMoviesDB-FreeStreaming-data", MovieEvent.FeedType.FreeVod)
                .AddMovieDbDataCheck("FxMoviesDB-PaidStreaming-data", MovieEvent.FeedType.PaidVod)
                .AddMovieDbDataCheck("FxMoviesDB-Streaming-VtmGo-data", MovieEvent.FeedType.FreeVod, "vtmgo")
                .AddMovieDbDataCheck("FxMoviesDB-Streaming-VrtNu-data", MovieEvent.FeedType.FreeVod, "vrtnu")
                .AddMovieDbDataCheck("FxMoviesDB-Streaming-GoPlay-data", MovieEvent.FeedType.FreeVod, "goplay")
                .AddMovieDbDataCheck("FxMoviesDB-Streaming-PrimeVideo-data", MovieEvent.FeedType.PaidVod, "primevideo")
                .AddMovieDbMissingImdbLinkCheck("FxMoviesDB-Broadcasts-missingImdbLink", MovieEvent.FeedType.Broadcast)
                .AddMovieDbMissingImdbLinkCheck("FxMoviesDB-FreeStreaming-missingImdbLink", MovieEvent.FeedType.FreeVod)
                .AddMovieDbMissingImdbLinkCheck("FxMoviesDB-PaidStreaming-missingImdbLink", MovieEvent.FeedType.PaidVod)
                .AddCheck<ImdbDbDateTimeCheck>("ImdbDB-datetime")
                .AddCheck<SystemInfoCheck>("SystemInfo");

        //services.AddHealthChecksUI();

        return services;
    }

    public static IApplicationBuilder UseFxMoviesHealthChecks(this IApplicationBuilder app)
    {
        var options = app.ApplicationServices.GetRequiredService<IOptions<HealthCheckOptions>>().Value;

        var uri = options.Uri;
        if (uri != null)
            app.UseHealthChecks(uri,
                new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
                {
                    ResponseWriter = WriteZabbixResponse
                });

        return app;
    }

    private static async Task WriteZabbixResponse(HttpContext httpContext, HealthReport result)
    {
        httpContext.Response.ContentType = "application/json";

        var zabbixResponse = new ZabbixResponse
        {
            status = (int)result.Status,
            statusText = result.Status.ToString(),
            results = result.Entries.Select(pair =>
                new ZabbixResponse.ResultItem
                {
                    name = pair.Key,
                    status = (int)pair.Value.Status,
                    statusText = pair.Value.Status.ToString(),
                    description = pair.Value.Description,
                    data = pair.Value.Data.ToDictionary(s => s.Key, s => s.Value)
                }).ToList()
        };

        await httpContext.Response.WriteAsJsonAsync(zabbixResponse);
    }

    #region ZabbixResponse JsonModel

    // Resharper disable All

    private class ZabbixResponse
    {
        public int status { get; set; }
        public string statusText { get; set; }
        public List<ResultItem> results { get; set; }

        public class ResultItem
        {
            public string name { get; set; }
            public int status { get; set; }
            public string statusText { get; set; }
            public string description { get; set; }
            public IDictionary<string, object> data { get; set; }
        }
    }

    #endregion
}