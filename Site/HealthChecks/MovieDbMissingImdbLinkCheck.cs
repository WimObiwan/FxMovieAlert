using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FxMovies.Core.Entities;
using FxMovies.MoviesDB;
using FxMovies.Site.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace FxMovies.Site.HealthChecks;

public static class MovieDbMissingImdbLinkCheckBuilderExtensions
{
    public static IHealthChecksBuilder AddMovieDbMissingImdbLinkCheck(
        this IHealthChecksBuilder builder,
        string name,
        MovieEvent.FeedType feedType,
        HealthStatus? failureStatus = default,
        IEnumerable<string> tags = default)
    {
        return builder.Add(new HealthCheckRegistration(
            name,
            sp => new MovieDbMissingImdbLinkCheck(
                sp.GetRequiredService<IOptions<HealthCheckOptions>>(),
                sp.GetRequiredService<IServiceScopeFactory>(),
                feedType),
            failureStatus,
            tags));
    }
}

public class MovieDbMissingImdbLinkCheck : IHealthCheck
{
    private readonly MovieEvent.FeedType _feedType;
    private readonly HealthCheckOptions _healthCheckOptions;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public MovieDbMissingImdbLinkCheck(IOptions<HealthCheckOptions> healthCheckOptions,
        IServiceScopeFactory serviceScopeFactory,
        MovieEvent.FeedType feedType)
    {
        _healthCheckOptions = healthCheckOptions.Value;
        _serviceScopeFactory = serviceScopeFactory;
        _feedType = feedType;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // Health checks are executed simultaneous, but with the same DataContext.
        // Seems a bug to me, workaround is using a separate service scope scope.
        // https://github.com/dotnet/aspnetcore/issues/14453

        using (var scope = _serviceScopeFactory.CreateScope())
        {
            var moviesDbContext = scope.ServiceProvider.GetRequiredService<MoviesDbContext>();

            var count = await moviesDbContext.MovieEvents
                .Where(me => me.Feed == _feedType)
                .CountAsync(me =>
                    (me.Movie == null || string.IsNullOrEmpty(me.Movie.ImdbId) && !me.Movie.ImdbIgnore) &&
                    me.Type == 1);

            HealthStatus status;
            if (count <= (_healthCheckOptions.CheckMissingImdbLinkCount ?? 7))
                status = HealthStatus.Healthy;
            else
                status = HealthStatus.Unhealthy;

            var result = new HealthCheckResult(status, null, null,
                new Dictionary<string, object>
                {
                    { "MissingImdbLinkCount", count }
                });

            return result;
        }
    }
}