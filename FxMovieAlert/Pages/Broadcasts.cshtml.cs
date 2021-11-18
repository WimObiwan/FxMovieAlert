using FxMovieAlert.Options;
using FxMovies.Core.Entities;
using FxMovies.Core.Repositories;
using FxMovies.FxMoviesDB;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace FxMovieAlert.Pages;

[AllowAnonymous]
public class BroadcastsModel : BroadcastsModelBase
{
    public BroadcastsModel(
        IOptions<SiteOptions> siteOptions,
        FxMoviesDbContext fxMoviesDbContext,
        IUsersRepository usersRepository)
        : base(MovieEvent.FeedType.Broadcast,
            siteOptions,
            fxMoviesDbContext,
            usersRepository)
    { }
}
