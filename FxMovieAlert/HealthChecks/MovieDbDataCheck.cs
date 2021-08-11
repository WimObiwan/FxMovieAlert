using FxMovies.FxMoviesDB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace FxMovieAlert.HealthChecks
{
    public static class MovieDbDataCheckBuilderExtensions
    {
        public static IHealthChecksBuilder AddMovieDbDataCheck(
            this IHealthChecksBuilder builder,
            string name,
            bool videoOnDemand,
            string channelCode = null,
            HealthStatus? failureStatus = default,
            IEnumerable<string> tags = default)
        {
            return builder.Add(new HealthCheckRegistration(
                name,
                sp => new MovieDbDataCheck(
                    sp.GetRequiredService<IConfiguration>(),
                    sp.GetRequiredService<IServiceScopeFactory>(),
                    videoOnDemand,
                    channelCode),
                failureStatus,
                tags));
        }
    }

    public class MovieDbDataCheck : IHealthCheck
    {
        private readonly IConfiguration configuration;
        private readonly IServiceScopeFactory serviceScopeFactory;
        private readonly bool videoOnDemand;
        private readonly string channelCode;

        public MovieDbDataCheck(
            IConfiguration configuration,
            IServiceScopeFactory serviceScopeFactory,
            bool videoOnDemand,
            string channelCode)
        {
            this.configuration = configuration;
            this.serviceScopeFactory = serviceScopeFactory;
            this.videoOnDemand = videoOnDemand;
            this.channelCode = channelCode;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            // Health checks are executed simultaneous, but with the same DataContext.
            // Seems a bug to me, workaround is using a separate service scope scope.
            // https://github.com/dotnet/aspnetcore/issues/14453

            using (var scope = serviceScopeFactory.CreateScope())
            {
                var fxMoviesDbContext = scope.ServiceProvider.GetRequiredService<FxMoviesDbContext>();
                
                DateTime lastMovieAddedTime = await fxMoviesDbContext.MovieEvents
                    .Where(me => me.Vod == videoOnDemand && me.AddedTime.HasValue && (channelCode == null || me.Channel.Code == channelCode))
                    .MaxAsync(me => me.AddedTime.Value);
                var lastMovieAddedDaysAgo = (DateTime.UtcNow - lastMovieAddedTime).TotalDays;
                
                HealthStatus status;
                if (lastMovieAddedDaysAgo >= this.configuration.GetValue("HealthCheck:CheckLastMovieAddedMoreThanDaysAgo", 1.1))
                    status = HealthStatus.Unhealthy;
                else
                    status = HealthStatus.Healthy;                

                var values = new Dictionary<string, object>()
                {
                    { "LastMovieAddedAge", lastMovieAddedDaysAgo },
                    { "LastMovieAddedTime", lastMovieAddedTime },
                };

                if (!videoOnDemand)
                {
                    DateTime lastMovieStartTime = await fxMoviesDbContext.MovieEvents
                        .Where(me => !me.Vod && (channelCode == null || me.Channel.Code == channelCode))
                        .MaxAsync(me => me.StartTime);
                    var lastMovieStartDaysFromNow = (lastMovieStartTime - DateTime.Now).TotalDays;

                    if (status == HealthStatus.Healthy && lastMovieStartDaysFromNow <= this.configuration.GetValue("HealthCheck:CheckLastMovieMoreThanDays", 4.0))
                        status = HealthStatus.Unhealthy;
                    else
                        status = HealthStatus.Healthy;

                    values.Add("LastMovieStartTimeAge", lastMovieStartDaysFromNow);
                    values.Add("LastMovieStartTime", lastMovieStartTime);
                }

                HealthCheckResult result = new HealthCheckResult(status, null, null, values);

                return result;
            }
        }
    }
}
