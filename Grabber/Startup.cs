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
            services.AddDbContextFactory<FxMovies.FxMoviesDB.FxMoviesDbContext>(options =>
                options.UseSqlite(Configuration.GetConnectionString("FxMoviesDB")));

            services.AddDbContextFactory<FxMovies.ImdbDB.ImdbDbContext>(options =>
                options.UseSqlite(Configuration.GetConnectionString("ImdbDb")));

            services.Configure<TheMovieDbServiceOptions>(Configuration.GetSection(TheMovieDbServiceOptions.Position));
            services.Configure<UpdateEpgCommandOptions>(Configuration.GetSection(UpdateEpgCommandOptions.Position));

            services.AddScoped<IMovieCreationHelper, MovieCreationHelper>();
            services.AddScoped<ITheMovieDbService, TheMovieDbService>();
            services.AddScoped<IHumoService, HumoService>();

            services.AddSingleton<IUpdateEpgCommand, UpdateEpgCommand>();
        }
    }
}
