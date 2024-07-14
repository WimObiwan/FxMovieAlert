using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FxMovies.Site.Pages;

public class AccountModel : PageModel
{
    public void OnGet()
    {
    }

    public async Task OnGetLogin(string returnUrl = "/")
    {
        await HttpContext.ChallengeAsync("Auth0", new AuthenticationProperties
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
            //RedirectUri = returnUrl
            // Should be fixed by: https://community.auth0.com/t/how-do-i-set-up-a-dynamic-allowed-callback-url/60268
            RedirectUri = "/",
            IsPersistent = true
        });
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    public async Task<IActionResult> OnGetAvatar(CancellationToken cancellationToken)
    {
        var picture = User.FindFirst("picture")?.Value;

        var httpClient = new HttpClient();
        using var response = await httpClient.GetAsync(picture, cancellationToken);

        Response.StatusCode = (int)response.StatusCode;
        foreach (var header in response.Headers) Response.Headers[header.Key] = header.Value.ToArray();

        foreach (var header in response.Content.Headers) Response.Headers[header.Key] = header.Value.ToArray();

        // SendAsync removes chunking from the response. This removes the header so it doesn't expect a chunked response.
        Response.Headers.Remove("transfer-encoding");
        await response.Content.CopyToAsync(Response.Body, cancellationToken);
        return new EmptyResult();
    }
}