using FxMovies.FxMoviesDB;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Collections.Generic;
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

        var lastMovieAge = (last - DateTime.Now).TotalDays;

        HealthStatus status;
        if (lastMovieAge <= this.configuration.GetValue("HealthCheck:CheckLastMovieMoreThanDays", 4.0))
            status = HealthStatus.Unhealthy;
        else
            status = HealthStatus.Healthy;

        HealthCheckResult result = new HealthCheckResult(status, null, null, 
            new Dictionary<string, object>() {
                { "LastMovieAge", lastMovieAge }
            });
        
        return Task.FromResult(result);
    }
}