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
    private readonly IConfiguration configuration;

    public ImdbDbDateTimeCheck(IConfiguration configuration)
    {
        this.configuration = configuration;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var connectionString = configuration.GetConnectionString("ImdbDb");

        var connectionStringBuilder = new DbConnectionStringBuilder();
        connectionStringBuilder.ConnectionString = connectionString;
        var filePath = connectionStringBuilder["Data Source"].ToString();
        var fileInfo = new FileInfo(filePath);
        var lastWriteTimeUtc = fileInfo.LastWriteTimeUtc;
        var ageDays = (DateTime.UtcNow - lastWriteTimeUtc).TotalDays;

        HealthStatus status;
        if (ageDays > 92.0)
            status = HealthStatus.Unhealthy;
        else
            status = HealthStatus.Healthy;

        var result = new HealthCheckResult(status, null, null,
            new Dictionary<string, object>
            {
                { "ImdbDbLastWriteTimeUtc", lastWriteTimeUtc },
                { "ImdbDbAgeDays", ageDays }
            });

        return Task.FromResult(result);
    }
}