using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FxMovies.FxMoviesDB;
using FxMovies.ImdbDB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FxMovies.Core
{
    public interface IUpdateAllImdbUsersDataCommand
    {
        Task<int> Run();
    }

    public class UpdateAllImdbUsersDataCommand : IUpdateAllImdbUsersDataCommand
    {
        private readonly ILogger<UpdateAllImdbUsersDataCommand> logger;
        private readonly IUpdateImdbUserDataCommand updateImdbUserDataCommand;
        private readonly IUsersRepository usersRepository;

        public UpdateAllImdbUsersDataCommand(ILogger<UpdateAllImdbUsersDataCommand> logger,
            IUpdateImdbUserDataCommand UpdateImdbUserDataCommand,
            IUsersRepository usersRepository)
        {
            this.logger = logger;
            this.updateImdbUserDataCommand = UpdateImdbUserDataCommand;
            this.usersRepository = usersRepository;
        }

        public async Task<int> Run()
        {
            await foreach (var imdbUserId in usersRepository.GetAllImdbUserIds())
            {
                try
                {
                    await updateImdbUserDataCommand.Run(imdbUserId, false);
                }
                catch (Exception x)
                {
                    logger.LogError(x, $"Failed to update ratings for ImdbUserId={imdbUserId}");
                }
            }
            return 0;
        }
   }
}