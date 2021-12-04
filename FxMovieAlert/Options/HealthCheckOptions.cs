using System.Collections.Generic;

namespace FxMovieAlert.Options;

public class HealthCheckOptions
{
    public static string Position => "HealthCheck";

    public string Uri { get; set; }
    public int? CheckMissingImdbLinkCount { get; set; }
    public Dictionary<string, double> CheckLastMovieAddedMoreThanDaysAgo { get; set; }
    public double? CheckLastMovieMoreThanDays { get; set; }
}