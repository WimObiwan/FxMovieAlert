﻿using FxMovieAlert.Options;
using FxMovies.Core.Entities;
using FxMovies.Core.Repositories;
using FxMovies.FxMoviesDB;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace FxMovieAlert.Pages;

[AllowAnonymous]
public class FreeStreamingModel : BroadcastsModelBase
{
    public FreeStreamingModel(
        IOptions<SiteOptions> siteOptions,
        FxMoviesDbContext fxMoviesDbContext,
        IUsersRepository usersRepository)
        : base(MovieEvent.FeedType.FreeVod,
            siteOptions,
            fxMoviesDbContext,
            usersRepository)
    {}
}
