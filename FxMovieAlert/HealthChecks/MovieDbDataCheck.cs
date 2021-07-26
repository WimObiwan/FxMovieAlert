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
    public abstract class MovieDbDataCheck : IHealthCheck
    {
        private readonly IConfiguration configuration;
        private readonly IServiceScopeFactory serviceScopeFactory;
        private readonly Expression<Func<MovieEvent, bool>> filter;

        public MovieDbDataCheck(IConfiguration configuration, IServiceScopeFactory serviceScopeFactory,
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
                
                DateTime last;
                last = await fxMoviesDbContext.MovieEvents
                    .Where(filter)
                    .MaxAsync(me => me.StartTime);

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
                
                return result;
            }
        }
    }

    public class MovieDbBroadcastsDataCheck : MovieDbDataCheck
    {
        public MovieDbBroadcastsDataCheck(IConfiguration configuration, IServiceScopeFactory serviceScopeFactory)
            : base(configuration, serviceScopeFactory, (me) => me.Vod == false)
        {
        }
    }

    public class MovieDbStreamingDataCheck : MovieDbDataCheck
    {
        public MovieDbStreamingDataCheck(IConfiguration configuration, IServiceScopeFactory serviceScopeFactory)
            : base(configuration, serviceScopeFactory, (me) => me.Vod == true)
        {
        }
    }
}
