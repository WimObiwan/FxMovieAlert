using System.Linq;

namespace FxMovieAlert
{
    public static class ClaimChecker
    {
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
