using System.Linq;

namespace FxMovieAlert
{
    public static class ClaimChecker
    {
        public static string UserId(System.Security.Principal.IIdentity identity)
        {
            if (!(identity is System.Security.Claims.ClaimsIdentity identity2))
            {
                return null;
            }

            return identity2.Claims
                .FirstOrDefault(c => c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")
                ?.Value;
        }

        public static bool Has(System.Security.Principal.IIdentity identity, string claim)
        {
            if (!(identity is System.Security.Claims.ClaimsIdentity identity2))
            {
                return false;
            }

            return identity2.Claims.Any(v => v.Value == claim);
        }
    }
}
