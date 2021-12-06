using FxMovies.Core;
using FxMovies.ImdbDB;
using FxMovies.MoviesDB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FxMovies.Grabber;

public class Startup
{
    private readonly IConfiguration configuration;

    public Startup(IConfiguration configuration)
    {
        this.configuration = configuration;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddDbContext<MoviesDbContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("FxMoviesDB")));

        services.AddDbContext<ImdbDbContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("ImdbDb")));

        services.AddFxMoviesCore(configuration, typeof(Program).Assembly);
    }
}