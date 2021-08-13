using FxMovies.Core;
using FxMovies.FxMoviesDB;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace FxMovieAlert.Pages
{
    [AllowAnonymous]
    public class StreamingModel : BroadcastModelBase
    {
        public StreamingModel(
            IConfiguration configuration,
            IOptions<SiteOptions> siteOptions,
            FxMoviesDbContext fxMoviesDbContext,
            IUsersRepository usersRepository)
            : base(true,
                configuration,
                siteOptions,
                fxMoviesDbContext,
                usersRepository)
        {}
    }
}
