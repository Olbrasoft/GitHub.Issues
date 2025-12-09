namespace Olbrasoft.GitHub.Issues.Sync.Services;

public interface IGitHubSyncService
{
    Task SyncRepositoryAsync(string owner, string repo, CancellationToken cancellationToken = default);
    Task SyncAllRepositoriesAsync(CancellationToken cancellationToken = default);
}
