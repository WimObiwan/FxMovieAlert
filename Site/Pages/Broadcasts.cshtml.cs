using FxMovies.Core.Entities;
using FxMovies.Core.Repositories;
using FxMovies.MoviesDB;
using FxMovies.Site.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace FxMovies.Site.Pages;

[AllowAnonymous]
public class BroadcastsModel : BroadcastsModelBase
{
    public BroadcastsModel(
        IOptions<SiteOptions> siteOptions,
        MoviesDbContext moviesDbContext,
        IUsersRepository usersRepository)
        : base(MovieEvent.FeedType.Broadcast,
            siteOptions,
            moviesDbContext,
            usersRepository)
    {
    }
}