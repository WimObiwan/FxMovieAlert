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
    public static class MovieDbMissingImdbLinkCheckBuilderExtensions
    {
        public static IHealthChecksBuilder AddMovieDbMissingImdbLinkCheck(
            this IHealthChecksBuilder builder,
            string name,
            bool videoOnDemand,
            HealthStatus? failureStatus = default,
            IEnumerable<string> tags = default)
        {
            return builder.Add(new HealthCheckRegistration(
                name,
                sp => new MovieDbMissingImdbLinkCheck(
                    sp.GetRequiredService<IConfiguration>(),
                    sp.GetRequiredService<IServiceScopeFactory>(),
                    videoOnDemand),
                failureStatus,
                tags));
        }
    }

    public class MovieDbMissingImdbLinkCheck : IHealthCheck
    {
        private readonly IConfiguration configuration;
        private readonly IServiceScopeFactory serviceScopeFactory;
        private readonly bool videoOnDemand;

        public MovieDbMissingImdbLinkCheck(IConfiguration configuration, IServiceScopeFactory serviceScopeFactory, 
            bool videoOnDemand)
        {
            this.configuration = configuration;
            this.serviceScopeFactory = serviceScopeFactory;
            this.videoOnDemand = videoOnDemand;
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
                
                int count = await fxMoviesDbContext.MovieEvents
                    .Where(me => me.Vod == videoOnDemand)
                    .CountAsync(me => (me.Movie == null || (string.IsNullOrEmpty(me.Movie.ImdbId) && !me.Movie.ImdbIgnore)) && me.Type == 1);

                HealthStatus status;
                if (count <= this.configuration.GetValue("HealthCheck:CheckMissingImdbLinkCount", 7))
                    status = HealthStatus.Healthy;
                else
                    status = HealthStatus.Unhealthy;

                HealthCheckResult result = new HealthCheckResult(status, null, null, 
                    new Dictionary<string, object>() {
                        { "MissingImdbLinkCount", count }
                    });
                
                return result;
            }
        }
    }
}