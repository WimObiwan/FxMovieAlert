using FxMovies.Core;
using FxMovies.FxMoviesDB;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace FxMovieAlert.Pages
{
    [AllowAnonymous]
    public class BroadcastModel : BroadcastModelBase
    {
        public BroadcastModel(
            IConfiguration configuration,
            IOptions<SiteOptions> siteOptions,
            FxMoviesDbContext fxMoviesDbContext,
            IUsersRepository usersRepository)
            : base(false,
                configuration,
                siteOptions,
                fxMoviesDbContext,
                usersRepository)
        { }
    }
}
