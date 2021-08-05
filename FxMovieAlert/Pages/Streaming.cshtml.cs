using FxMovies.Core;
using FxMovies.FxMoviesDB;
using FxMovies.ImdbDB;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;

namespace FxMovieAlert.Pages
{
    [AllowAnonymous]
    public class StreamingModel : BroadcastModelBase
    {
        public StreamingModel(
            IConfiguration configuration,
            FxMoviesDbContext fxMoviesDbContext,
            IUsersRepository usersRepository)
            : base(true,
                configuration,
                fxMoviesDbContext,
                usersRepository)
        {}
    }
}
