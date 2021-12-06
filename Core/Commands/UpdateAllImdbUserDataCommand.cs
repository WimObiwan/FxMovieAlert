using System;
using System.Threading.Tasks;
using FxMovies.Core.Repositories;
using Microsoft.Extensions.Logging;

namespace FxMovies.Core.Commands;

public interface IUpdateAllImdbUsersDataCommand
{
    Task<int> Execute();
}

public class UpdateAllImdbUsersDataCommand : IUpdateAllImdbUsersDataCommand
{
    private readonly ILogger<UpdateAllImdbUsersDataCommand> _logger;
    private readonly IUpdateImdbUserDataCommand _updateImdbUserDataCommand;
    private readonly IUsersRepository _usersRepository;

    public UpdateAllImdbUsersDataCommand(ILogger<UpdateAllImdbUsersDataCommand> logger,
        IUpdateImdbUserDataCommand updateImdbUserDataCommand,
        IUsersRepository usersRepository)
    {
        _logger = logger;
        _updateImdbUserDataCommand = updateImdbUserDataCommand;
        _usersRepository = usersRepository;
    }

    public async Task<int> Execute()
    {
        await foreach (var imdbUserId in _usersRepository.GetAllImdbUserIds())
            try
            {
                await _updateImdbUserDataCommand.Execute(imdbUserId, false);
            }
            catch (Exception x)
            {
                _logger.LogError(x, "Failed to update ratings for ImdbUserId {ImdbUserId}", imdbUserId);
            }

        return 0;
    }
}