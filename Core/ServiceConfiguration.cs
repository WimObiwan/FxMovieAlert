using System;
using System.Reflection;
using FxMovies.Core.Commands;
using FxMovies.Core.Queries;
using FxMovies.Core.Repositories;
using FxMovies.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FxMovies.Core
{
    public static class ServiceConfiguration
    {
        public static IServiceCollection AddFxMoviesCore(this IServiceCollection services, IConfiguration configuration, Assembly assembly)
        {
            services.AddHttpClient("humo", c =>
            {
                c.BaseAddress = new Uri("https://www.humo.be");
            });
            services.AddHttpClient("goplay", c =>
            {
                c.BaseAddress = new Uri("https://www.goplay.be");
            });
            services.AddHttpClient("vtmgo_login", c =>
            {
                c.BaseAddress = new Uri("https://login2.vtm.be");
                c.DefaultRequestHeaders.Add("User-Agent", "VTMGO/10.3 (be.vmma.vtm.zenderapp; build:13259; Android 25) okhttp/4.9.0");
                c.DefaultRequestHeaders.Add("x-app-version", "10");
                c.DefaultRequestHeaders.Add("x-persgroep-mobile-app", "true");
                c.DefaultRequestHeaders.Add("x-persgroep-os", "android");
                c.DefaultRequestHeaders.Add("x-persgroep-os-version", "25");
            });
            services.AddHttpClient("vtmgo_dpg", c =>
            {
                c.BaseAddress = new Uri("https://lfvp-api.dpgmedia.net");
                c.DefaultRequestHeaders.Add("User-Agent", "VTMGO/10.3 (be.vmma.vtm.zenderapp; build:13259; Android 25) okhttp/4.9.0");
                c.DefaultRequestHeaders.Add("x-app-version", "10");
                c.DefaultRequestHeaders.Add("x-persgroep-mobile-app", "true");
                c.DefaultRequestHeaders.Add("x-persgroep-os", "android");
                c.DefaultRequestHeaders.Add("x-persgroep-os-version", "25");
            });
            services.AddHttpClient("vrtnu", c =>
            {
                c.BaseAddress = new Uri("https://search.vrt.be");
            });
            services.AddHttpClient("tmdb", c => 
            {
                c.BaseAddress = new Uri("https://api.themoviedb.org");
            });
            services.AddHttpClient("images");
            services.AddHttpClient("imdb", c => 
            {
                c.BaseAddress = new Uri("https://www.imdb.com");
            });
            services.AddHttpClient("primevideo", c =>
            {
                c.BaseAddress = new Uri("https://www.primevideo.com");
            });

            services.AddSingleton<IVersionInfo, VersionInfo>((_) => new VersionInfo(assembly));

            services.AddScoped<IUpdateEpgCommand, UpdateEpgCommand>();
            services.AddScoped<IGenerateImdbDatabaseCommand, GenerateImdbDatabaseCommand>();
            services.AddScoped<IUpdateImdbUserDataCommand, UpdateImdbUserDataCommand>();
            services.AddScoped<IUpdateImdbLinkCommand, UpdateImdbLinkCommand>();
            services.AddScoped<IUpdateAllImdbUsersDataCommand, UpdateAllImdbUsersDataCommand>();
            services.AddScoped<IAutoUpdateImdbUserDataCommand, AutoUpdateImdbUserDataCommand>();

            services.AddScoped<IImdbMatchingQuery, ImdbMatchingQuery>();
            services.AddScoped<IManualMatchesQuery, ManualMatchesQuery>();
            services.AddScoped<IListManualMatchesQuery, ListManualMatchesQuery>();
            services.AddScoped<IStatsQuery, StatsQuery>();

            services.Configure<TheMovieDbServiceOptions>(configuration.GetSection(TheMovieDbServiceOptions.Position));
            services.Configure<VtmGoServiceOptions>(configuration.GetSection(VtmGoServiceOptions.Position));
            services.Configure<PrimeVideoServiceOptions>(configuration.GetSection(PrimeVideoServiceOptions.Position));
            services.Configure<UpdateEpgCommandOptions>(configuration.GetSection(UpdateEpgCommandOptions.Position));
            services.Configure<GenerateImdbDatabaseCommandOptions>(configuration.GetSection(GenerateImdbDatabaseCommandOptions.Position));
            services.Configure<AutoUpdateImdbUserDataCommandOptions>(configuration.GetSection(AutoUpdateImdbUserDataCommandOptions.Position));
            services.Configure<ImdbMatchingQueryOptions>(configuration.GetSection(ImdbMatchingQueryOptions.Position));

            services.AddScoped<IMovieCreationHelper, MovieCreationHelper>();
            services.AddScoped<IImdbRatingsFromWebService, ImdbRatingsFromWebService>();
            services.AddScoped<IImdbRatingsFromFileService, ImdbRatingsFromFileService>();
            services.AddScoped<IImdbWatchlistFromWebService, ImdbWatchlistFromWebService>();
            services.AddScoped<IImdbWatchlistFromFileService, ImdbWatchlistFromFileService>();
            services.AddScoped<ITheMovieDbService, TheMovieDbService>();
            services.AddScoped<IMovieEventService, GoPlayService>();
            services.AddScoped<IMovieEventService, VtmGoService>();
            services.AddScoped<IMovieEventService, VrtNuService>();
            services.AddScoped<IMovieEventService, PrimeVideoService>();
            services.AddScoped<IHumoService, HumoService>();

            services.AddScoped<IUserRatingsRepository, UserRatingsRepository>();
            services.AddScoped<IUserWatchlistRepository, UserWatchlistRepository>();
            services.AddScoped<IUsersRepository, UsersRepository>();

            return services;
        }
    }
}