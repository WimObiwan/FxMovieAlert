﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FxMovies.Core;
using FxMovies.FxMoviesDB;
using FxMovies.ImdbDB;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FxMovieAlert.Pages
{
    [AllowAnonymous]
    public class BroadcastModel : BroadcastModelBase
    {
        public BroadcastModel(IConfiguration configuration, FxMoviesDbContext fxMoviesDbContext, ImdbDbContext imdbDbContext,
            IMovieCreationHelper movieCreationHelper)
            : base(false, configuration, fxMoviesDbContext, imdbDbContext, movieCreationHelper)
        {}
    }
}
