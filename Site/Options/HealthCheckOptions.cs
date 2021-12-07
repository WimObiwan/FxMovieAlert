using System.Collections.Generic;

namespace FxMovies.Site.Options;

// Resharper disable All
public class HealthCheckOptions
{
    public static string Position => "HealthCheck";

    public string Uri { get; set; }
    public int? CheckMissingImdbLinkCount { get; set; }
    public Dictionary<string, double> CheckLastMovieAddedMoreThanDaysAgo { get; set; }
    public double? CheckLastMovieMoreThanDays { get; set; }
}

// Resharper restore All