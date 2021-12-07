using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FxMovies.Site.HealthChecks;

public class ImdbDbDateTimeCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;

    public ImdbDbDateTimeCheck(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var connectionString = _configuration.GetConnectionString("ImdbDb");

        var connectionStringBuilder = new DbConnectionStringBuilder
        {
            ConnectionString = connectionString
        };
        var filePath = connectionStringBuilder["Data Source"].ToString();
        HealthStatus status;
        Dictionary<string, object> data;
        if (filePath != null)
        {
            var fileInfo = new FileInfo(filePath);
            var lastWriteTimeUtc = fileInfo.LastWriteTimeUtc;
            var ageDays = (DateTime.UtcNow - lastWriteTimeUtc).TotalDays;

            status = ageDays > 92.0 ? HealthStatus.Unhealthy : HealthStatus.Healthy;

            data = new Dictionary<string, object>
            {
                { "ImdbDbLastWriteTimeUtc", lastWriteTimeUtc },
                { "ImdbDbAgeDays", ageDays }
            };
        }
        else
        {
            status = HealthStatus.Unhealthy;
            data = null;
        }

        var result = new HealthCheckResult(status, null, null, data);
        return Task.FromResult(result);
    }
}