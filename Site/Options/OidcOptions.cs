using System.Diagnostics.CodeAnalysis;

namespace FxMovies.Site.Options;

[ExcludeFromCodeCoverage]
public class OidcOptions
{
    public static string Position => "Oidc";

    public string Domain { get; set; }
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }
}