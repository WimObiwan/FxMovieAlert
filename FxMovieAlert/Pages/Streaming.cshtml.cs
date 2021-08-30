using FxMovieAlert.Options;
using FxMovies.Core.Repositories;
using FxMovies.FxMoviesDB;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace FxMovieAlert.Pages
{
    [AllowAnonymous]
    public class StreamingModel : BroadcastModelBase
    {
        public StreamingModel(
            IOptions<SiteOptions> siteOptions,
            FxMoviesDbContext fxMoviesDbContext,
            IUsersRepository usersRepository)
            : base(true,
                siteOptions,
                fxMoviesDbContext,
                usersRepository)
        {}
    }
}
