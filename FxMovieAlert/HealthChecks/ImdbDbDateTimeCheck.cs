using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace FxMovieAlert.HealthChecks;

public class ImdbDbDateTimeCheck : IHealthCheck
{
    private readonly IConfiguration configuration;

    public ImdbDbDateTimeCheck(IConfiguration configuration)
    {
        this.configuration = configuration;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        var connectionString = configuration.GetConnectionString("ImdbDb");

        var connectionStringBuilder = new DbConnectionStringBuilder(); 
        connectionStringBuilder.ConnectionString = connectionString;
        string filePath = connectionStringBuilder["Data Source"].ToString();
        var fileInfo = new System.IO.FileInfo(filePath);
        var lastWriteTimeUtc = fileInfo.LastWriteTimeUtc;
        var ageDays = (DateTime.UtcNow - lastWriteTimeUtc).TotalDays;

        HealthStatus status;
        if (ageDays > 92.0)
            status = HealthStatus.Unhealthy;
        else
            status = HealthStatus.Healthy;

        HealthCheckResult result = new HealthCheckResult(status, null, null, 
                new Dictionary<string, object>() {
                    { "ImdbDbLastWriteTimeUtc", lastWriteTimeUtc },
                    { "ImdbDbAgeDays", ageDays }
                });
            
        return Task.FromResult(result);
    }
}
