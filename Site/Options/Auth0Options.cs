using System.Diagnostics.CodeAnalysis;

namespace FxMovies.Site.Options;

[ExcludeFromCodeCoverage]
public class Auth0Options
{
    public static string Position => "Auth0";

    public string Domain { get; set; }
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }
}