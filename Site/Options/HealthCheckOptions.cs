using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace FxMovies.Site.Options;

// Resharper disable All
[ExcludeFromCodeCoverage]
public class HealthCheckOptions
{
    public static string Position => "HealthCheck";

    public string Uri { get; set; }
    public int? CheckMissingImdbLinkCount { get; set; }
    public Dictionary<string, double> CheckLastMovieAddedMoreThanDaysAgo { get; set; }
    public double? CheckLastMovieMoreThanDays { get; set; }
}

// Resharper restore All