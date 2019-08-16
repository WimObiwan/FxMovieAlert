using FxMovies.FxMoviesDB;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
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

        string message = $"Missing Imdb link count is {count}";

        if (count <= this.configuration.GetValue("HealthCheck:CheckMissingImdbLinkCount", 0))
        {
            return Task.FromResult(HealthCheckResult.Healthy(message));
        }

        return Task.FromResult(HealthCheckResult.Unhealthy(message));
    }
}