using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FxMovieAlert.Pages
{
    public class AccountModel : PageModel
    {
        public string Message { get; set; }

        public void OnGet()
        {
        }

        public async Task OnGetLogin(string returnUrl = "/")
        {
            await HttpContext.ChallengeAsync("Auth0", new AuthenticationProperties()
            {
                RedirectUri = returnUrl,
                IsPersistent = true,
                AllowRefresh = true
            });
        }

        public async Task OnGetLogout(string returnUrl = "/")
        {
            await HttpContext.SignOutAsync("Auth0", new AuthenticationProperties
            {
                RedirectUri = returnUrl
            });
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}
