using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using FxMovies.Core;
using FxMovieAlert.HealthChecks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Net.Http.Headers;

namespace FxMovieAlert
{
    public class Startup
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public Startup(IConfiguration configuration, IWebHostEnvironment environment)
        {
            _configuration = configuration;
            _environment = environment;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            string pathToCryptoKeys = Path.Join(_environment.ContentRootPath, "dp_keys");
            Directory.CreateDirectory(pathToCryptoKeys);
            services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(pathToCryptoKeys));

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
                options.ExpireTimeSpan = TimeSpan.FromDays(31);

                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.None;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            })
            .AddOpenIdConnect("Auth0", options => {
                // Set the authority to your Auth0 domain
                //options.Authority = $"https://{_configuration["Auth0:Domain"]}";
                options.Authority = $"https://{_configuration["Auth0:Domain"]}";

                // Configure the Auth0 Client ID and Client Secret
                options.ClientId = _configuration["Auth0:ClientId"];
                options.ClientSecret = _configuration["Auth0:ClientSecret"];

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
                        var logoutUri = $"https://{_configuration["Auth0:Domain"]}/v2/logout?client_id={_configuration["Auth0:ClientId"]}";

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
                    sqliteConnectionString: _configuration.GetConnectionString("FxMoviesDB"), 
                    name: "sqlite-FxMoviesDB")
                // .AddSqlite(
                //     sqliteConnectionString: _configuration.GetConnectionString("FxMoviesHistoryDb"), 
                //     name: "sqlite-FxMoviesHistoryDb")
                .AddSqlite(
                    sqliteConnectionString: _configuration.GetConnectionString("ImdbDb"),
                    name: "sqlite-ImdbDb")
                .AddIdentityServer(
                    idSvrUri: new Uri($"https://{_configuration["Auth0:Domain"]}"),
                    name: "idsvr-Auth0")
                .AddMovieDbDataCheck("FxMoviesDB-Broadcasts-data", false)
                .AddMovieDbDataCheck("FxMoviesDB-Streaming-data", true)
                .AddMovieDbDataCheck("FxMoviesDB-Streaming-VtmGo-data", true, "vtmgo")
                .AddMovieDbDataCheck("FxMoviesDB-Streaming-VrtNu-data", true, "vrtnu")
                .AddMovieDbMissingImdbLinkCheck("FxMoviesDB-Broadcasts-missingImdbLink", false)
                .AddMovieDbMissingImdbLinkCheck("FxMoviesDB-Streaming-missingImdbLink", true)
                .AddCheck<ImdbDbDateTimeCheck>("ImdbDB-datetime");


            //services.AddHealthChecksUI();
            
            // Add framework services.
            services.AddRazorPages();

            services.AddWebOptimizer();

            services.AddDbContext<FxMovies.FxMoviesDB.FxMoviesDbContext>(options =>
                options.UseSqlite(_configuration.GetConnectionString("FxMoviesDB")));

            services.AddDbContext<FxMovies.ImdbDB.ImdbDbContext>(options =>
                options.UseSqlite(_configuration.GetConnectionString("ImdbDb")));

            services.Configure<TheMovieDbServiceOptions>(_configuration.GetSection(TheMovieDbServiceOptions.Position));

            services.AddSingleton<IVersionInfo, VersionInfo>((_) => new VersionInfo(typeof(Startup).Assembly));
            services.AddScoped<IUserRatingsRepository, UserRatingsRepository>();
            services.AddScoped<IUsersRepository, UsersRepository>();
            services.AddScoped<IUserWatchlistRepository, UserWatchlistRepository>();
            services.AddScoped<IImdbRatingsFromFileService, ImdbRatingsFromFileService>();
            services.AddScoped<IImdbWatchlistFromFileService, ImdbWatchlistFromFileService>();
            services.AddScoped<IMovieCreationHelper, MovieCreationHelper>();
            services.AddScoped<ITheMovieDbService, TheMovieDbService>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app)
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

            if (_environment.IsDevelopment())
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

            app.UseHealthChecks(_configuration.GetValue("HealthCheck:Uri", "/hc"), new HealthCheckOptions()
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

        private class ZabbixResponse
        {
            public class ResultItem
            {
                public string name { get; set; }
                public int status { get; set; }
                public string statusText { get; set; }
                public string description { get; set; }
                public IDictionary<string, object> data { get; set; }
            }

            public int status { get; set; }
            public string statusText { get; set; }
            public List<ResultItem> results { get; set; }            
        }

        private static async Task WriteZabbixResponse(HttpContext httpContext, HealthReport result)
        {
            httpContext.Response.ContentType = "application/json";

            var zabbixResponse = new ZabbixResponse()
            {
                status = (int)result.Status,
                statusText = result.Status.ToString(),
                results = result.Entries.Select(pair => 
                    new ZabbixResponse.ResultItem()
                    {
                        name = pair.Key,
                        status = (int)pair.Value.Status,
                        statusText = pair.Value.Status.ToString(),
                        description = pair.Value.Description,
                        data = pair.Value.Data.ToDictionary((s) => s.Key, (s) => s.Value)
                    }).ToList()
            };

            await httpContext.Response.WriteAsJsonAsync(zabbixResponse);
        }
    }
}
