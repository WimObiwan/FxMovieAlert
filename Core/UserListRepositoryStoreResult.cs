namespace FxMovies.Core
{
    public class UserListRepositoryStoreResult
    {
        public int ExistingCount { get; internal set; }
        public int NewCount { get; internal set; }
        public int RemovedCount { get; internal set; }
        public string LastTitle { get; internal set; }
    }
}
