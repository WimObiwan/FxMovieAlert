namespace FxMovieAlert
{
    public class SiteOptions
    {
        public static string Position => "Site";

        public string SentryBrowserDsn { get; set; }
        public string GoogleAnalyticsPropertyId { get; set; }
        public string GoogleAdsensePublishId { get; set; }
        public string GoogleAdsenseVerticleAdSlot {get; set; }
        public int AdsInterval { get; set; }
    }
}