namespace Olbrasoft.GitHub.Issues.Sync.Services;

public interface IGitHubSyncService
{
    /// <summary>
    /// Synchronizes a single repository.
    /// </summary>
    Task SyncRepositoryAsync(string owner, string repo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronizes all repositories based on configuration:
    /// - If Repositories list is not empty, uses explicit list
    /// - Otherwise, if Owner is set, discovers repos via API
    /// </summary>
    Task SyncAllRepositoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronizes a list of repositories provided as arguments.
    /// </summary>
    Task SyncRepositoriesAsync(IEnumerable<string> repositories, CancellationToken cancellationToken = default);
}
