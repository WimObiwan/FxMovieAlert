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
    private readonly FxMoviesDbContext fxMoviesDbContext;

    public MovieDbDataCheck(IConfiguration configuration, FxMoviesDbContext fxMoviesDbContext)
    {
        this.configuration = configuration;
        this.fxMoviesDbContext = fxMoviesDbContext;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        DateTime last;
        last = fxMoviesDbContext.MovieEvents.Max(me => me.StartTime);

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