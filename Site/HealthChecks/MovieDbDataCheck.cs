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

public static class MovieDbDataCheckBuilderExtensions
{
    public static IHealthChecksBuilder AddMovieDbDataCheck(
        this IHealthChecksBuilder builder,
        string name,
        MovieEvent.FeedType feedType,
        string channelCode = null,
        HealthStatus? failureStatus = default,
        IEnumerable<string> tags = default)
    {
        return builder.Add(new HealthCheckRegistration(
            name,
            sp => new MovieDbDataCheck(
                sp.GetRequiredService<IServiceScopeFactory>(),
                feedType,
                channelCode),
            failureStatus,
            tags));
    }
}

public class MovieDbDataCheck : IHealthCheck
{
    private readonly string _channelCode;
    private readonly MovieEvent.FeedType _feedType;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public MovieDbDataCheck(
        IServiceScopeFactory serviceScopeFactory,
        MovieEvent.FeedType feedType,
        string channelCode)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _feedType = feedType;
        _channelCode = channelCode;
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

        var query = moviesDbContext.MovieEvents
            .Where(me =>
                me.Feed == _feedType && me.AddedTime.HasValue &&
                (_channelCode == null || me.Channel.Code == _channelCode));
        var count = await query.CountAsync(cancellationToken);
        DateTime? lastMovieAddedTime;
        if (count > 0)
            lastMovieAddedTime = await query.MaxAsync(me => me.AddedTime, cancellationToken);
        else
            lastMovieAddedTime = null;
        var lastMovieAddedDaysAgo = (DateTime.UtcNow - lastMovieAddedTime)?.TotalDays;

        // Use IOptionsSnapshot<T> instead of IOptions<T> to avoid caching
        var healthCheckOptions = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<HealthCheckOptions>>().Value;
        const double checkLastMovieAddedMoreThanDaysAgoDefault = 1.1;
        double checkLastMovieAddedMoreThanDaysAgo;
        if (healthCheckOptions.CheckLastMovieAddedMoreThanDaysAgo == null)
            checkLastMovieAddedMoreThanDaysAgo = checkLastMovieAddedMoreThanDaysAgoDefault;
        else if (_channelCode == null ||
                 !healthCheckOptions.CheckLastMovieAddedMoreThanDaysAgo.TryGetValue(_channelCode,
                     out checkLastMovieAddedMoreThanDaysAgo))
            if (!healthCheckOptions.CheckLastMovieAddedMoreThanDaysAgo.TryGetValue($"FeedType-{_feedType}",
                    out checkLastMovieAddedMoreThanDaysAgo))
                if (!healthCheckOptions.CheckLastMovieAddedMoreThanDaysAgo.TryGetValue("",
                        out checkLastMovieAddedMoreThanDaysAgo))
                    checkLastMovieAddedMoreThanDaysAgo = checkLastMovieAddedMoreThanDaysAgoDefault;

        HealthStatus status;
        if (!lastMovieAddedDaysAgo.HasValue || lastMovieAddedDaysAgo.Value >= checkLastMovieAddedMoreThanDaysAgo)
            status = HealthStatus.Unhealthy;
        else
            status = HealthStatus.Healthy;

        var values = new Dictionary<string, object>
        {
            { "LastMovieAddedAge", lastMovieAddedDaysAgo },
            { "LastMovieAddedTime", lastMovieAddedTime },
            { "Count", count },
            { "AlarmThreshold", checkLastMovieAddedMoreThanDaysAgo }
        };

        if (_feedType == MovieEvent.FeedType.Broadcast)
        {
            var lastMovieStartTime = await query.MaxAsync(me => me.StartTime, cancellationToken);
            var lastMovieStartDaysFromNow = (lastMovieStartTime - DateTime.Now).TotalDays;

            if (status == HealthStatus.Healthy &&
                lastMovieStartDaysFromNow <= (healthCheckOptions.CheckLastMovieMoreThanDays ?? 4.0))
                status = HealthStatus.Unhealthy;
            else
                status = HealthStatus.Healthy;

            values.Add("LastMovieStartTimeAge", lastMovieStartDaysFromNow);
            values.Add("LastMovieStartTime", lastMovieStartTime);
        }

        var result = new HealthCheckResult(status, null, null, values);

        return result;
    }
}