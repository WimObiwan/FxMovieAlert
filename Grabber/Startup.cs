using FxMovies.Core;
using FxMovies.FxMoviesDB;
using FxMovies.ImdbDB;
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
        services.AddDbContext<FxMoviesDbContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("FxMoviesDB")));

        services.AddDbContext<ImdbDbContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("ImdbDb")));

        services.AddFxMoviesCore(configuration, typeof(Program).Assembly);
    }
}