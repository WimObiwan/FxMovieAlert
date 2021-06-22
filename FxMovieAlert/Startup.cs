using System;
using System.Globalization;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Linq;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using FxMovies.Core;
using Microsoft.Net.Http.Headers;

namespace FxMovieAlert
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Add authentication services
            services.Configure<CookiePolicyOptions>(options => {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = Microsoft.AspNetCore.Http.SameSiteMode.None;
            });

            services.AddAuthentication(options => {
                options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                // add an instance of the patched manager to the options:
                options.CookieManager = new ChunkingCookieManager();

                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.None;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            })
            .AddOpenIdConnect("Auth0", options => {
                // Set the authority to your Auth0 domain
                //options.Authority = $"https://{Configuration["Auth0:Domain"]}";
                options.Authority = $"https://{Configuration["Auth0:Domain"]}";

                // Configure the Auth0 Client ID and Client Secret
                options.ClientId = Configuration["Auth0:ClientId"];
                options.ClientSecret = Configuration["Auth0:ClientSecret"];

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
                    OnTicketReceived = context =>
                    {
                        // Get the ClaimsIdentity
                        var identity = context.Principal.Identity as ClaimsIdentity;
                        if (identity != null)
                        {
                                // Add the Name ClaimType. This is required if we want User.Identity.Name to actually return something!
                                if (!context.Principal.HasClaim(c => c.Type == ClaimTypes.Name) &&
                                    identity.HasClaim(c => c.Type == "name"))
                                identity.AddClaim(new Claim(ClaimTypes.Name, identity.FindFirst("name").Value));

                                // Check if token names are stored in Properties
                                if (context.Properties.Items.ContainsKey(".TokenNames"))
                                {   
                                    // Token names a semicolon separated
                                    string[] tokenNames = context.Properties.Items[".TokenNames"].Split(';');

                                    // Add each token value as Claim
                                    foreach (var tokenName in tokenNames)
                                    {
                                        // Tokens are stored in a Dictionary with the Key ".Token.<token name>"
                                        string tokenValue = context.Properties.Items[$".Token.{tokenName}"];
                                        identity.AddClaim(new Claim(tokenName, tokenValue));
                                    }
                                }
                        }

                        return Task.CompletedTask;
                    },

                    // handle the logout redirection 
                    OnRedirectToIdentityProviderForSignOut = (context) =>
                    {
                        var logoutUri = $"https://{Configuration["Auth0:Domain"]}/v2/logout?client_id={Configuration["Auth0:ClientId"]}";

                        var postLogoutUri = context.Properties.RedirectUri;
                        if (!string.IsNullOrEmpty(postLogoutUri))
                        {
                            if (postLogoutUri.StartsWith("/"))
                            {
                                // transform to absolute
                                var request = context.Request;
                                postLogoutUri = request.Scheme + "://" + request.Host + request.PathBase + postLogoutUri;
                            }
                            logoutUri += $"&returnTo={ Uri.EscapeDataString(postLogoutUri)}";
                        }

                        context.Response.Redirect(logoutUri);
                        context.HandleResponse();

                        return Task.CompletedTask;
                    }
                };   
            });


            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = 
                    ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            });

            services.AddHealthChecks()
                .AddSqlite(
                    sqliteConnectionString: Configuration.GetConnectionString("FxMoviesDB"), 
                    name: "sqlite-FxMoviesDB")
                // .AddSqlite(
                //     sqliteConnectionString: Configuration.GetConnectionString("FxMoviesHistoryDb"), 
                //     name: "sqlite-FxMoviesHistoryDb")
                .AddSqlite(
                    sqliteConnectionString: Configuration.GetConnectionString("ImdbDb"),
                    name: "sqlite-ImdbDb")
                .AddIdentityServer(
                    idSvrUri: new Uri($"https://{Configuration["Auth0:Domain"]}"),
                    name: "idsvr-Auth0")
                .AddCheck<MovieDbBroadcastsDataCheck>("FxMoviesDB-Broadcasts-data")
                .AddCheck<MovieDbStreamingDataCheck>("FxMoviesDB-Streaming-data")
                .AddCheck<MovieDbBroadcastsMissingImdbLinkCheck>("FxMoviesDB-Broadcasts-missingImdbLink")
                .AddCheck<MovieDbStreamingMissingImdbLinkCheck>("FxMoviesDB-Streaming-missingImdbLink");

            //services.AddHealthChecksUI();
            
            // Add framework services.
            services.AddRazorPages();

            services.AddWebOptimizer();

            services.AddDbContext<FxMovies.FxMoviesDB.FxMoviesDbContext>(options =>
                options.UseSqlite(Configuration.GetConnectionString("FxMoviesDB")));

            services.AddDbContext<FxMovies.ImdbDB.ImdbDbContext>(options =>
                options.UseSqlite(Configuration.GetConnectionString("ImdbDb")));

            services.Configure<TheMovieDbServiceOptions>(Configuration.GetSection(TheMovieDbServiceOptions.Position));
            services.AddScoped<IMovieCreationHelper, MovieCreationHelper>();
            services.AddScoped<ITheMovieDbService, TheMovieDbService>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            // https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-2.1
            app.UseForwardedHeaders();

            var supportedCultures = new[]
            {
                new CultureInfo("nl-BE"),
                new CultureInfo("nl"),
            };

            app.UseRequestLocalization(new RequestLocalizationOptions
            {
                DefaultRequestCulture = new RequestCulture(new CultureInfo("nl-BE")),
                // Formatting numbers, dates, etc.
                SupportedCultures = supportedCultures,
                // UI strings that we have localized.
                SupportedUICultures = supportedCultures
            });

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                //app.UseHsts(); // handles by nginx
            }

            // //forward headers from the LB
            // var forwardOpts = new ForwardedHeadersOptions
            // {
            //     ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor
            // };
            // //TODO: Set this up to only accept the forwarded headers from the load balancer
            // forwardOpts.KnownNetworks.Clear();
            // forwardOpts.KnownProxies.Clear();
            // app.UseForwardedHeaders(forwardOpts);

            //app.UseHttpsRedirection(); // handles by nginx
            app.UseWebOptimizer();

            app.UseStaticFiles(new StaticFileOptions
            {
                OnPrepareResponse = ctx =>
                {
                    const int durationInSeconds = 60 * 60 * 24;
                    ctx.Context.Response.Headers[HeaderNames.CacheControl] = "public,max-age=" + durationInSeconds;
                }
            });
            app.UseCookiePolicy();

            app.UseAuthentication();

            app.UseHealthChecks(Configuration.GetValue("HealthCheck:Uri", "/hc"), new HealthCheckOptions()
            {
                Predicate = _ => true,
                //ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
                ResponseWriter = WriteZabbixResponse
            });
            // app.UseHealthChecksUI(o => 
            // {
            //     o.UIPath = "/hc-ui";
            //     o.ApiPath = "/hc";
            // });

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapRazorPages();
                endpoints.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
            });
        }

        private static Task WriteZabbixResponse(HttpContext httpContext, HealthReport result)
        {
            httpContext.Response.ContentType = "application/json";

            var json = new JObject(
                new JProperty("status", result.Status),
                new JProperty("statusText", result.Status.ToString()),
                new JProperty("results", new JArray(result.Entries.Select(pair =>
                {
                    List<JProperty> properties = new List<JProperty>();
                    properties.Add(new JProperty("name", pair.Key));
                    properties.Add(new JProperty("status", pair.Value.Status));
                    properties.Add(new JProperty("statusText", pair.Value.Status.ToString()));
                    if (pair.Value.Description != null) 
                        properties.Add(new JProperty("description", pair.Value.Description));
                    if (pair.Value.Data.Any())
                        properties.Add(new JProperty("data", 
                            new JObject(pair.Value.Data.Select(p => new JProperty(p.Key, p.Value)))));
                    return new JObject(properties);
                }))));
            return httpContext.Response.WriteAsync(json.ToString(Formatting.None));
        }
    }
}
