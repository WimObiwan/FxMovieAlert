using FxMovieAlert.Options;
using FxMovies.Core.Entities;
using FxMovies.Core.Repositories;
using FxMovies.FxMoviesDB;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace FxMovieAlert.Pages
{
    [AllowAnonymous]
    public class PayingModel : BroadcastModelBase
    {
        public PayingModel(
            IOptions<SiteOptions> siteOptions,
            FxMoviesDbContext fxMoviesDbContext,
            IUsersRepository usersRepository)
            : base(MovieEvent.FeedType.PaidVod,
                siteOptions,
                fxMoviesDbContext,
                usersRepository)
        {}
    }
}
