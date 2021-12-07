namespace FxMovies.Core.Repositories;

public class UserListRepositoryStoreResult
{
    public int ExistingCount { get; internal init; }
    public int NewCount { get; internal init; }
    public int RemovedCount { get; internal init; }
    public string LastTitle { get; internal init; }
}