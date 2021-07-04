using System;
using System.Net;
using System.Net.Http;
using FxMovies.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FxMovies.Grabber
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<FxMovies.FxMoviesDB.FxMoviesDbContext>(options =>
                options.UseSqlite(Configuration.GetConnectionString("FxMoviesDB")));

            services.AddDbContext<FxMovies.ImdbDB.ImdbDbContext>(options =>
                options.UseSqlite(Configuration.GetConnectionString("ImdbDb")));

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
                c.BaseAddress = new Uri("https://vrtnu-api.vrt.be");
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

            services.AddScoped<IUpdateEpgCommand, UpdateEpgCommand>();
            services.AddScoped<IGenerateImdbDatabaseCommand, GenerateImdbDatabaseCommand>();
            services.AddScoped<IUpdateImdbUserRatingsCommand, UpdateImdbUserRatingsCommand>();

            services.Configure<TheMovieDbServiceOptions>(Configuration.GetSection(TheMovieDbServiceOptions.Position));
            services.Configure<VtmGoServiceOptions>(Configuration.GetSection(VtmGoServiceOptions.Position));
            services.Configure<UpdateEpgCommandOptions>(Configuration.GetSection(UpdateEpgCommandOptions.Position));
            services.Configure<GenerateImdbDatabaseCommandOptions>(Configuration.GetSection(GenerateImdbDatabaseCommandOptions.Position));

            services.AddScoped<IMovieCreationHelper, MovieCreationHelper>();
            services.AddScoped<IImdbRatingsService, ImdbRatingsService>();
            services.AddScoped<ITheMovieDbService, TheMovieDbService>();
            services.AddScoped<IVtmGoService, VtmGoService>();
            services.AddScoped<IVrtNuService, VrtNuService>();
            services.AddScoped<IHumoService, HumoService>();

            services.AddScoped<IUserRatingsRepository, UserRatingsRepository>();
        }
    }
}
