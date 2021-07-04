using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FxMovies.FxMoviesDB;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FxMovies.Core
{
    public interface IUsersRepository
    {
        IAsyncEnumerable<string> GetAllImdbUserIds();
    }

    public class UsersRepository : IUsersRepository
    {
        private readonly ILogger<UsersRepository> logger;
        private readonly FxMoviesDbContext fxMoviesDbContext;

        public UsersRepository(
            ILogger<UsersRepository> logger,
            FxMoviesDbContext fxMoviesDbContext)
        {
            this.logger = logger;
            this.fxMoviesDbContext = fxMoviesDbContext;
        }

        public IAsyncEnumerable<string> GetAllImdbUserIds()
        {
            return fxMoviesDbContext.Users.Select(u => u.ImdbUserId).AsAsyncEnumerable();
        }

    }
}