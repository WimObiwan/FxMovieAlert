using System;
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
                sp.GetRequiredService<IServiceScopeFactory>(),
                feedType),
            failureStatus,
            tags));
    }
}

public class MovieDbMissingImdbLinkCheck : IHealthCheck
{
    private readonly MovieEvent.FeedType _feedType;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public MovieDbMissingImdbLinkCheck(
        IServiceScopeFactory serviceScopeFactory,
        MovieEvent.FeedType feedType)
    {
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

        using var scope = _serviceScopeFactory.CreateScope();
        var moviesDbContext = scope.ServiceProvider.GetRequiredService<MoviesDbContext>();

        var dbMovieEvents =
            moviesDbContext.MovieEvents.Where(me => me.Feed == _feedType || me.Feed == null && me.Vod == true);

        DateTime now = DateTime.Now;

        if (_feedType == MovieEvent.FeedType.Broadcast)
            dbMovieEvents = dbMovieEvents.Where(me =>
                me.EndTime >= now &&
                me.StartTime >= now.AddMinutes(-30));
        else
            dbMovieEvents = dbMovieEvents.Where(me => me.EndTime == null || me.EndTime >= now);

        var count = await dbMovieEvents
            .Where(me => me.Feed == _feedType)
            .CountAsync(me =>
                (me.Movie == null || string.IsNullOrEmpty(me.Movie.ImdbId) && !me.Movie.ImdbIgnore) &&
                me.Type == 1, cancellationToken);

        // Use IOptionsSnapshot<T> instead of IOptions<T> to avoid caching
        var healthCheckOptions = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<HealthCheckOptions>>().Value;
        var status = count <= (healthCheckOptions.CheckMissingImdbLinkCount ?? 7)
            ? HealthStatus.Healthy
            : HealthStatus.Unhealthy;

        var result = new HealthCheckResult(status, null, null,
            new Dictionary<string, object>
            {
                { "MissingImdbLinkCount", count }
            });

        return result;
    }
}