using FxMovies.Core;
using FxMovies.ImdbDB;
using FxMovies.MoviesDB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FxMovies.Grabber;

public class Startup
{
    private readonly IConfiguration _configuration;

    public Startup(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddDbContext<MoviesDbContext>(options =>
            options.UseSqlite(_configuration.GetConnectionString("FxMoviesDB")));

        services.AddDbContext<ImdbDbContext>(options =>
            options.UseSqlite(_configuration.GetConnectionString("ImdbDb")));

        services.AddFxMoviesCore(_configuration, typeof(Program).Assembly);
    }
}