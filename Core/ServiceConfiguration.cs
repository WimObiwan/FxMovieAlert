using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using FxMovies.Core.Commands;
using FxMovies.Core.Queries;
using FxMovies.Core.Repositories;
using FxMovies.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FxMovies.Core;

[ExcludeFromCodeCoverage]
public static class ServiceConfiguration
{
    public static IServiceCollection AddFxMoviesCore(this IServiceCollection services, IConfiguration configuration,
        Assembly assembly)
    {
        services.AddMemoryCache();

        services.AddHttpClient("humo", c => { c.BaseAddress = new Uri("https://www.humo.be"); });
        services.AddHttpClient("goplay", c => { c.BaseAddress = new Uri("https://www.goplay.be"); });
        services.AddHttpClient("vtmgo", c => { c.BaseAddress = new Uri("https://vtm.be/vtmgo/"); });
        services.AddHttpClient("vtmgo_login", c =>
        {
            c.BaseAddress = new Uri("https://login2.vtm.be");
            c.DefaultRequestHeaders.Add("User-Agent",
                "VTMGO/10.3 (be.vmma.vtm.zenderapp; build:13259; Android 25) okhttp/4.9.0");
            c.DefaultRequestHeaders.Add("x-app-version", "10");
            c.DefaultRequestHeaders.Add("x-persgroep-mobile-app", "true");
            c.DefaultRequestHeaders.Add("x-persgroep-os", "android");
            c.DefaultRequestHeaders.Add("x-persgroep-os-version", "25");
        });
        services.AddHttpClient("vtmgo_dpg", c =>
        {
            c.BaseAddress = new Uri("https://lfvp-api.dpgmedia.net");
            c.DefaultRequestHeaders.Add("User-Agent",
                "VTMGO/10.3 (be.vmma.vtm.zenderapp; build:13259; Android 25) okhttp/4.9.0");
            c.DefaultRequestHeaders.Add("x-app-version", "10");
            c.DefaultRequestHeaders.Add("x-persgroep-mobile-app", "true");
            c.DefaultRequestHeaders.Add("x-persgroep-os", "android");
            c.DefaultRequestHeaders.Add("x-persgroep-os-version", "25");
        });
        services.AddHttpClient("vrtmax",
            c => {
                c.BaseAddress = new Uri("https://www.vrt.be/");

                // Set headers
                c.DefaultRequestHeaders.Add("accept", "application/graphql-response+json, application/graphql+json, application/json, text/event-stream, multipart/mixed");
                c.DefaultRequestHeaders.Add("accept-language", "nl");
                c.DefaultRequestHeaders.Add("cache-control", "no-cache");
                c.DefaultRequestHeaders.Add("dnt", "1");
                c.DefaultRequestHeaders.Add("origin", "https://www.vrt.be");
                c.DefaultRequestHeaders.Add("pragma", "no-cache");
                c.DefaultRequestHeaders.Add("referer", "https://www.vrt.be/vrtmax/films/");
                c.DefaultRequestHeaders.Add("sec-ch-ua", "\"Chromium\";v=\"128\", \"Not;A=Brand\";v=\"24\", \"Microsoft Edge\";v=\"128\"");
                c.DefaultRequestHeaders.Add("sec-ch-ua-mobile", "?1");
                c.DefaultRequestHeaders.Add("sec-ch-ua-platform", "\"Android\"");
                c.DefaultRequestHeaders.Add("sec-fetch-dest", "empty");
                c.DefaultRequestHeaders.Add("sec-fetch-mode", "cors");
                c.DefaultRequestHeaders.Add("sec-fetch-site", "same-origin");
                c.DefaultRequestHeaders.Add("user-agent", "Mozilla/5.0 (Linux; Android 8.0.0; SM-G955U Build/R16NW) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/116.0.0.0 Mobile Safari/537.36 Edg/128.0.0.0");
                c.DefaultRequestHeaders.Add("x-vrt-client-name", "WEB");
                c.DefaultRequestHeaders.Add("x-vrt-client-version", "1.5.14");
                c.DefaultRequestHeaders.Add("x-vrt-zone", "default");
            }
        );
        services.AddHttpClient("tmdb", c => { c.BaseAddress = new Uri("https://api.themoviedb.org"); });
        services.AddHttpClient("images");
        services.AddHttpClient("imdb", c => { c.BaseAddress = new Uri("https://www.imdb.com"); });
        services.AddHttpClient("primevideo", c => { c.BaseAddress = new Uri("https://www.primevideo.com"); });

        services.AddSingleton<IVersionInfo, VersionInfo>(_ => new VersionInfo(assembly));

        services.AddScoped<IUpdateEpgCommand, UpdateEpgCommand>();
        services.AddScoped<IGenerateImdbDatabaseCommand, GenerateImdbDatabaseCommand>();
        services.AddScoped<IUpdateImdbUserDataCommand, UpdateImdbUserDataCommand>();
        services.AddScoped<IUpdateImdbLinkCommand, UpdateImdbLinkCommand>();
        services.AddScoped<IUpdateAllImdbUsersDataCommand, UpdateAllImdbUsersDataCommand>();
        services.AddScoped<IAutoUpdateImdbUserDataCommand, AutoUpdateImdbUserDataCommand>();

        services.AddScoped<IBroadcastQuery, BroadcastQuery>();
        services.AddScoped<ICachedBroadcastQuery, CachedBroadcastQuery>();
        services.AddScoped<IImdbMatchingQuery, ImdbMatchingQuery>();
        services.AddScoped<IListManualMatchesQuery, ListManualMatchesQuery>();
        services.AddScoped<IManualMatchesQuery, ManualMatchesQuery>();
        services.AddScoped<IStatsQuery, StatsQuery>();

        services.Configure<CachedBroadcastQueryOptions>(configuration.GetSection(CachedBroadcastQueryOptions.Position));
        services.Configure<TheMovieDbServiceOptions>(configuration.GetSection(TheMovieDbServiceOptions.Position));
        services.Configure<PrimeVideoServiceOptions>(configuration.GetSection(PrimeVideoServiceOptions.Position));
        services.Configure<UpdateEpgCommandOptions>(configuration.GetSection(UpdateEpgCommandOptions.Position));
        services.Configure<GenerateImdbDatabaseCommandOptions>(
            configuration.GetSection(GenerateImdbDatabaseCommandOptions.Position));
        services.Configure<AutoUpdateImdbUserDataCommandOptions>(
            configuration.GetSection(AutoUpdateImdbUserDataCommandOptions.Position));
        services.Configure<ImdbMatchingQueryOptions>(configuration.GetSection(ImdbMatchingQueryOptions.Position));

        services.AddScoped<IMovieCreationHelper, MovieCreationHelper>();
        services.AddScoped<IImdbRatingsFromWebService, ImdbRatingsFromWebService>();
        services.AddScoped<IImdbRatingsFromFileService, ImdbRatingsFromFileService>();
        services.AddScoped<IImdbWatchlistFromWebService, ImdbWatchlistFromWebService>();
        services.AddScoped<IImdbWatchlistFromFileService, ImdbWatchlistFromFileService>();
        services.AddScoped<ITheMovieDbService, TheMovieDbService>();
        services.AddScoped<IMovieEventService, GoPlayService>();
        //services.AddScoped<IMovieEventService, VtmGoService>();
        services.AddScoped<IMovieEventService, VtmGoService2>();
        services.AddScoped<IMovieEventService, VrtMaxService>();
        services.AddScoped<IMovieEventService, PrimeVideoService>();
        services.AddScoped<IHumoService, HumoService>();

        services.AddScoped<IUserRatingsRepository, UserRatingsRepository>();
        services.AddScoped<IUserWatchlistRepository, UserWatchlistRepository>();
        services.AddScoped<IUsersRepository, UsersRepository>();

        return services;
    }
}