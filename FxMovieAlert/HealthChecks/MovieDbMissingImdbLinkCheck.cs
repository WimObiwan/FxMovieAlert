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
    public abstract class MovieDbMissingImdbLinkCheck : IHealthCheck
    {
        private readonly IConfiguration configuration;
        private readonly IServiceScopeFactory serviceScopeFactory;
        private readonly Expression<Func<MovieEvent, bool>> filter;

        public MovieDbMissingImdbLinkCheck(IConfiguration configuration, IServiceScopeFactory serviceScopeFactory, 
            Expression<Func<MovieEvent, bool>> filter)
        {
            this.configuration = configuration;
            this.serviceScopeFactory = serviceScopeFactory;
            this.filter = filter;
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
                    .Where(filter)
                    .CountAsync(me => string.IsNullOrEmpty(me.Movie.ImdbId) && me.Type == 1);

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

    public class MovieDbBroadcastsMissingImdbLinkCheck : MovieDbMissingImdbLinkCheck
    {
        public MovieDbBroadcastsMissingImdbLinkCheck(IConfiguration configuration, IServiceScopeFactory serviceScopeFactory)
            : base(configuration, serviceScopeFactory, (MovieEvent me) => me.Vod == false)
        {
        }
    }

    public class MovieDbStreamingMissingImdbLinkCheck : MovieDbMissingImdbLinkCheck
    {
        public MovieDbStreamingMissingImdbLinkCheck(IConfiguration configuration, IServiceScopeFactory serviceScopeFactory)
            : base(configuration, serviceScopeFactory, (MovieEvent me) => me.Vod == true)
        {
        }
    }
}