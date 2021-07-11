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
            ImdbDbContext imdbDbContext,
            IMovieCreationHelper movieCreationHelper,
            IUsersRepository usersRepository)
            : base(false,
                configuration,
                fxMoviesDbContext,
                imdbDbContext,
                movieCreationHelper,
                usersRepository)
        { }
    }
}
