using System.Linq;
using System.Security.Claims;
using System.Security.Principal;

namespace FxMovieAlert;

public static class ClaimChecker
{
    public static string UserId(IIdentity identity)
    {
        if (!(identity is ClaimsIdentity identity2)) return null;

        return identity2.Claims
            .FirstOrDefault(c => c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")
            ?.Value;
    }

    public static bool Has(IIdentity identity, string claim)
    {
        if (!(identity is ClaimsIdentity identity2)) return false;

        return identity2.Claims.Any(v => v.Value == claim);
    }
}