using System;
using System.Security.Claims;
using System.Threading.Tasks;
using FxMovies.Site.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FxMovies.Site;

internal static class AuthenticationApplicationBuilderExtensions
{
    public static IServiceCollection AddFxMoviesAuthentication(this IServiceCollection services,
        Auth0Options auth0Options)
    {
        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                // add an instance of the patched manager to the options:
                options.CookieManager = new ChunkingCookieManager();
                options.ExpireTimeSpan = TimeSpan.FromDays(31);
                options.SlidingExpiration = true;

                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.None;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            })
            .AddOpenIdConnect("Auth0", options =>
            {
                // Set the authority to your Auth0 domain
                options.Authority = $"https://{auth0Options.Domain}";

                // Configure the Auth0 Client ID and Client Secret
                options.ClientId = auth0Options.ClientId;
                options.ClientSecret = auth0Options.ClientSecret;

                // Set response type to code
                options.ResponseType = "code";

                options.SaveTokens = true;

                // Configure the scope
                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");

                // Set the callback path, so Auth0 will call back to http://localhost:5000/signin-auth0 
                // Also ensure that you have added the URL as an Allowed Callback URL in your Auth0 dashboard 
                options.CallbackPath = new PathString("/signin-auth0");

                // Configure the Claims Issuer to be Auth0
                options.ClaimsIssuer = "Auth0";

                options.Events = new OpenIdConnectEvents
                {
                    OnTicketReceived = OnTicketReceived,
                    OnRedirectToIdentityProviderForSignOut =
                        ctx => OnRedirectToIdentityProviderForSignOut(ctx, auth0Options)
                };
            });

        return services;
    }

    private static Task OnTicketReceived(TicketReceivedContext context)
    {
        // Get the ClaimsIdentity
        if (context.Principal?.Identity is ClaimsIdentity identity)
        {
            // Add the Name ClaimType. This is required if we want User.Identity.Name to actually return something!
            if (!context.Principal.HasClaim(c => c.Type == ClaimTypes.Name))
            {
                var name = identity.FindFirst("name")?.Value;
                if (name != null)
                    identity.AddClaim(new Claim(ClaimTypes.Name, name));
            }

            // Check if token names are stored in Properties
            var items = context.Properties?.Items;
            if (items != null
                && items.TryGetValue(".TokenNames", out var tokenNamesString)
                && tokenNamesString != null)
            {
                // Token names a semicolon separated
                var tokenNames = tokenNamesString.Split(';');

                // Add each token value as Claim
                foreach (var tokenName in tokenNames)
                    // Tokens are stored in a Dictionary with the Key ".Token.<token name>"
                    if (items.TryGetValue($".Token.{tokenName}", out var tokenValue) &&
                        tokenValue != null)
                        identity.AddClaim(new Claim(tokenName, tokenValue));
            }
        }

        return Task.CompletedTask;
    }

    private static Task OnRedirectToIdentityProviderForSignOut(RedirectContext context, Auth0Options auth0Options)
    {
        var logoutUri =
            $"https://{auth0Options.Domain}/v2/logout?client_id={auth0Options.ClientId}";

        var postLogoutUri = context.Properties.RedirectUri;
        if (!string.IsNullOrEmpty(postLogoutUri))
        {
            if (postLogoutUri.StartsWith("/"))
            {
                // transform to absolute
                var request = context.Request;
                postLogoutUri = request.Scheme + "://" + request.Host + request.PathBase +
                                postLogoutUri;
            }

            logoutUri += $"&returnTo={Uri.EscapeDataString(postLogoutUri)}";
        }

        context.Response.Redirect(logoutUri);
        context.HandleResponse();

        return Task.CompletedTask;
    }
}