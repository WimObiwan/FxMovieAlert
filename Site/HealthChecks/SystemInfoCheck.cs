using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FxMovies.Core;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FxMovies.Site.HealthChecks;

public class SystemInfoCheck : IHealthCheck
{
    private readonly IVersionInfo _versionInfo;

    public SystemInfoCheck(IVersionInfo versionInfo)
    {
        _versionInfo = versionInfo;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var result = new HealthCheckResult(HealthStatus.Healthy, null, null,
            new Dictionary<string, object>
            {
                { "Version", _versionInfo.Version },
                { "DotNetCoreVersion", _versionInfo.DotNetCoreVersion },
                { "MachineName", Environment.MachineName }
            });

        return Task.FromResult(result);
    }
}