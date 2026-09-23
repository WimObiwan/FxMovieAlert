using System;
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

    // Neutral placeholder, served whenever the user has no usable picture. Kept inline
    // rather than as a wwwroot asset so there is no second thing that can be missing.
    private const string DefaultAvatarSvg =
        """
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64" role="img" aria-label="avatar">
          <circle cx="32" cy="32" r="32" fill="#d8d8d8"/>
          <circle cx="32" cy="25" r="12" fill="#9e9e9e"/>
          <path d="M8 64c0-13.3 10.7-24 24-24s24 10.7 24 24z" fill="#9e9e9e"/>
        </svg>
        """;

    // Reused on purpose: a new HttpClient per request leaks sockets under load.
    private static readonly HttpClient AvatarHttpClient = new() { Timeout = TimeSpan.FromSeconds(5) };

    private IActionResult DefaultAvatar()
    {
        Response.Headers.CacheControl = "public, max-age=3600";
        return Content(DefaultAvatarSvg, "image/svg+xml");
    }

    public async Task<IActionResult> OnGetAvatar(CancellationToken cancellationToken)
    {
        var picture = User.FindFirst("picture")?.Value;

        // Not every identity provider supplies a picture. Keycloak only emits the claim if
        // the user has a "picture" attribute (populated by an attribute importer on the
        // google IdP); password users have none at all. A missing claim must not throw -
        // _Layout always renders this <img>, so an exception here breaks every page.
        if (string.IsNullOrWhiteSpace(picture))
            return DefaultAvatar();

        // This endpoint fetches a URL taken from an external token, server-side, and returns
        // the body to the browser - an SSRF vector. Restricting it to absolute https URLs
        // keeps it away from loopback and link-local services reachable from this host.
        if (!Uri.TryCreate(picture, UriKind.Absolute, out var pictureUri)
            || pictureUri.Scheme != Uri.UriSchemeHttps)
            return DefaultAvatar();

        HttpResponseMessage response;
        try
        {
            response = await AvatarHttpClient.GetAsync(pictureUri,
                HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // client disconnected - let the framework deal with it
        }
        catch (Exception)
        {
            // Timeout, DNS failure, TLS problem, refused connection: degrade to the
            // placeholder rather than failing the page that embeds this image.
            return DefaultAvatar();
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
                return DefaultAvatar();

            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType == null
                || !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return DefaultAvatar();

            // Only the content type is forwarded. The previous version copied every
            // upstream header verbatim, which is why it then had to strip
            // transfer-encoding again.
            Response.ContentType = contentType;
            Response.Headers.CacheControl = "private, max-age=3600";
            await response.Content.CopyToAsync(Response.Body, cancellationToken);
        }

        return new EmptyResult();
    }
}