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

            services.AddFxMoviesCore(configuration, typeof(Program).Assembly);
        }
    }
}
