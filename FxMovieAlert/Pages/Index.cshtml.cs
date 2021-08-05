using FxMovies.Core;
using FxMovies.FxMoviesDB;
using FxMovies.ImdbDB;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;

namespace FxMovieAlert.Pages
{
    [AllowAnonymous]
    public class BroadcastModel : BroadcastModelBase
    {
        public BroadcastModel(
            IConfiguration configuration,
            FxMoviesDbContext fxMoviesDbContext,
            IUsersRepository usersRepository)
            : base(false,
                configuration,
                fxMoviesDbContext,
                usersRepository)
        { }
    }
}
