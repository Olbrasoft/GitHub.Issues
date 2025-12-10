using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Sync.Services;

/// <summary>
/// Service for synchronizing labels from GitHub.
/// </summary>
public interface ILabelSyncService
{
    /// <summary>
    /// Synchronizes labels from GitHub for a repository.
    /// </summary>
    Task SyncLabelsAsync(
        Repository repository,
        string owner,
        string repo,
        CancellationToken cancellationToken = default);
}
