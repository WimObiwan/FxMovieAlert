using FxMovies.Core;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FxMovieAlert.HealthChecks
{
    public class SystemInfoCheck : IHealthCheck
    {
        private readonly IVersionInfo versionInfo;

        public SystemInfoCheck(IVersionInfo versionInfo)
        {
            this.versionInfo = versionInfo;
        }

        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            HealthCheckResult result = new HealthCheckResult(HealthStatus.Healthy, null, null, 
                    new Dictionary<string, object>() {
                        { "Version", versionInfo.Version },
                        { "DotNetCoreVersion", versionInfo.DotNetCoreVersion },
                        { "MachineName", System.Environment.MachineName }
                    });
                
            return Task.FromResult(result);
        }
    }
}