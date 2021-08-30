using System;
using System.Threading.Tasks;
using FxMovies.Core.Repositories;
using Microsoft.Extensions.Logging;

namespace FxMovies.Core.Commands
{
    public interface IUpdateAllImdbUsersDataCommand
    {
        Task<int> Execute();
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

        public async Task<int> Execute()
        {
            await foreach (var imdbUserId in usersRepository.GetAllImdbUserIds())
            {
                try
                {
                    await updateImdbUserDataCommand.Execute(imdbUserId, false);
                }
                catch (Exception x)
                {
                    logger.LogError(x, "Failed to update ratings for ImdbUserId {ImdbUserId}", imdbUserId);
                }
            }
            return 0;
        }
   }
}