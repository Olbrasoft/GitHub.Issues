namespace Olbrasoft.GitHub.Issues.Sync.Services;

public interface IGitHubSyncService
{
    /// <summary>
    /// Synchronizes a single repository.
    /// </summary>
    /// <param name="owner">Repository owner</param>
    /// <param name="repo">Repository name</param>
    /// <param name="since">If provided, only sync issues changed since this timestamp (incremental). If null, full sync.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SyncRepositoryAsync(string owner, string repo, DateTimeOffset? since = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronizes all repositories based on configuration:
    /// - If Repositories list is not empty, uses explicit list
    /// - Otherwise, if Owner is set, discovers repos via API
    /// </summary>
    /// <param name="since">If provided, only sync issues changed since this timestamp (incremental). If null, full sync.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SyncAllRepositoriesAsync(DateTimeOffset? since = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronizes a list of repositories provided as arguments.
    /// </summary>
    /// <param name="repositories">List of repositories in "owner/repo" format</param>
    /// <param name="since">If provided, only sync issues changed since this timestamp (incremental). If null, full sync.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SyncRepositoriesAsync(IEnumerable<string> repositories, DateTimeOffset? since = null, CancellationToken cancellationToken = default);
}
