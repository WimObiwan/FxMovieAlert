using FxMovies.FxMoviesDB;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class MovieDbDataCheck : IHealthCheck
{
    private IConfiguration configuration;

    public MovieDbDataCheck(IConfiguration configuration)
    {
        this.configuration = configuration;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        DateTime last;

        string connectionString = configuration.GetConnectionString("FxMoviesDb");
        using (var db = FxMoviesDbContextFactory.Create(connectionString))
        {
            last = db.MovieEvents.Max(me => me.StartTime);
        }

        string message = $"Last movie is on {last}";
        if (last > DateTime.Now.AddDays(this.configuration.GetValue("HealthCheck:CheckLastMovieMoreThanDays", 9.0)))
        {
            return Task.FromResult(HealthCheckResult.Healthy(message));
        }

        return Task.FromResult(HealthCheckResult.Unhealthy(message));
    }
}