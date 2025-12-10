using System;
using System.Diagnostics.CodeAnalysis;

namespace FxMovies.Site.Options;

[ExcludeFromCodeCoverage]
public class RateLimitOptions
{
    public static string Position => "RateLimiting";

    public int PermitLimit { get; set; }
    public int Window { get; set; }
    public int QueueLimit { get; set; }
    public int PermitLimitPerMinute { get; set; }
    public int WindowMinute { get; set; }
    public int PermitLimitPerHour { get; set; }
    public int WindowHour { get; set; }
    public string[] WhitelistedIPs { get; set; } = Array.Empty<string>();
}
