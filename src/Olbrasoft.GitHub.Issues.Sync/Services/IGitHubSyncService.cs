namespace Olbrasoft.GitHub.Issues.Sync.Services;

public interface IGitHubSyncService
{
    /// <summary>
    /// Synchronizes a single repository.
    /// Uses incremental sync by default (only issues changed since last sync).
    /// </summary>
    /// <param name="owner">Repository owner</param>
    /// <param name="repo">Repository name</param>
    /// <param name="fullRefresh">If true, ignores last sync timestamp and re-syncs everything</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SyncRepositoryAsync(string owner, string repo, bool fullRefresh = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronizes all repositories based on configuration:
    /// - If Repositories list is not empty, uses explicit list
    /// - Otherwise, if Owner is set, discovers repos via API
    /// </summary>
    /// <param name="fullRefresh">If true, ignores last sync timestamp and re-syncs everything</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SyncAllRepositoriesAsync(bool fullRefresh = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronizes a list of repositories provided as arguments.
    /// </summary>
    /// <param name="repositories">List of repositories in "owner/repo" format</param>
    /// <param name="fullRefresh">If true, ignores last sync timestamp and re-syncs everything</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SyncRepositoriesAsync(IEnumerable<string> repositories, bool fullRefresh = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-embeds all existing issues with title + body combined.
    /// </summary>
    Task ReEmbedAllIssuesAsync(CancellationToken cancellationToken = default);
}
