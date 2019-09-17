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
    private IConfiguration configuration;

    public MovieDbMissingImdbLinkCheck(IConfiguration configuration)
    {
        this.configuration = configuration;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        int count;

        string connectionString = configuration.GetConnectionString("FxMoviesDb");
        using (var db = FxMoviesDbContextFactory.Create(connectionString))
        {
            count = db.MovieEvents.Count(me => string.IsNullOrEmpty(me.ImdbId) && me.Type == 1);
        }

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