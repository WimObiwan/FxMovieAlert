using FxMovies.Core;
using FxMovies.Core.Commands;
using FxMovies.Core.Queries;
using FxMovies.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FxMovies.Grabber
{
    public class Startup
    {
        private readonly IConfiguration configuration;

        public Startup(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<FxMovies.FxMoviesDB.FxMoviesDbContext>(options =>
                options.UseSqlite(configuration.GetConnectionString("FxMoviesDB")));

            services.AddDbContext<FxMovies.ImdbDB.ImdbDbContext>(options =>
                options.UseSqlite(configuration.GetConnectionString("ImdbDb")));

            services.Configure<TheMovieDbServiceOptions>(configuration.GetSection(TheMovieDbServiceOptions.Position));
            services.Configure<VtmGoServiceOptions>(configuration.GetSection(VtmGoServiceOptions.Position));
            services.Configure<UpdateEpgCommandOptions>(configuration.GetSection(UpdateEpgCommandOptions.Position));
            services.Configure<GenerateImdbDatabaseCommandOptions>(configuration.GetSection(GenerateImdbDatabaseCommandOptions.Position));
            services.Configure<AutoUpdateImdbUserDataCommandOptions>(configuration.GetSection(AutoUpdateImdbUserDataCommandOptions.Position));
            services.Configure<ImdbMatchingQueryOptions>(configuration.GetSection(ImdbMatchingQueryOptions.Position));

            services.AddFxMoviesCore(configuration, typeof(Program).Assembly);
        }
    }
}
