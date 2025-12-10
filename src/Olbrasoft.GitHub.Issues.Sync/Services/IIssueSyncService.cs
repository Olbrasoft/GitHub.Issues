using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Sync.Services;

/// <summary>
/// Service for synchronizing issues from GitHub.
/// </summary>
public interface IIssueSyncService
{
    /// <summary>
    /// Synchronizes issues from GitHub for a repository.
    /// </summary>
    Task SyncIssuesAsync(
        Repository repository,
        string owner,
        string repo,
        DateTimeOffset? since = null,
        CancellationToken cancellationToken = default);
}
