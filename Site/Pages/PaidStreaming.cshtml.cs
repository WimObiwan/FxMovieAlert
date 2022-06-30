using FxMovies.Core.Entities;
using FxMovies.Core.Queries;
using FxMovies.Core.Repositories;
using FxMovies.MoviesDB;
using FxMovies.Site.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace FxMovies.Site.Pages;

[AllowAnonymous]
public class PaidStreamingModel : BroadcastsModelBase
{
    public PaidStreamingModel(
        IOptions<SiteOptions> siteOptions,
        MoviesDbContext moviesDbContext,
        IBroadcastQuery broadcastQuery,
        IUsersRepository usersRepository)
        : base(MovieEvent.FeedType.PaidVod,
            siteOptions,
            moviesDbContext,
            broadcastQuery,
            usersRepository)
    {
    }
}