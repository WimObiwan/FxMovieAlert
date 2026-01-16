using System.Diagnostics.CodeAnalysis;

namespace FxMovies.Site.Options;

[ExcludeFromCodeCoverage]
public class SearchBotVerificationOptions
{
    public static string Position => "SearchBotVerification";

    /// <summary>
    /// Duration in minutes to cache bot verification results. Default is 15 minutes.
    /// </summary>
    public int CacheDurationMinutes { get; set; } = 15;
}
