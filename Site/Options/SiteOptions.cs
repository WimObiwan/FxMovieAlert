using System.Diagnostics.CodeAnalysis;

namespace FxMovies.Site.Options;

[ExcludeFromCodeCoverage]
public class SiteOptions
{
    public static string Position => "Site";

    public string SentryBrowserDsn { get; set; }
    public string GoogleAnalyticsPropertyId { get; set; }
    public string GoogleAnalyticsPropertyId2 { get; set; }
    public string GoogleAdsensePublishId { get; set; }
    public string GoogleAdsenseVerticleAdSlot { get; set; }
    public int AdsInterval { get; set; }
    public int DefaultMaxDays { get; set; }
}