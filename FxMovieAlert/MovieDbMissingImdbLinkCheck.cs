using FxMovies.FxMoviesDB;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class MovieDbMissingImdbLinkCheck : IHealthCheck
{
    private readonly IConfiguration configuration;
    private readonly FxMoviesDbContext fxMoviesDbContext;

    public MovieDbMissingImdbLinkCheck(IConfiguration configuration, FxMoviesDbContext fxMoviesDbContext)
    {
        this.configuration = configuration;
        this.fxMoviesDbContext = fxMoviesDbContext;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        int count = fxMoviesDbContext.MovieEvents.Count(me => string.IsNullOrEmpty(me.Movie.ImdbId) && me.Type == 1);

        HealthStatus status;
        if (count <= this.configuration.GetValue("HealthCheck:CheckMissingImdbLinkCount", 15))
            status = HealthStatus.Healthy;
        else
            status = HealthStatus.Unhealthy;

        HealthCheckResult result = new HealthCheckResult(status, null, null, 
            new Dictionary<string, object>() {
                { "MissingImdbLinkCount", count }
            });
        
        return Task.FromResult(result);
    }
}