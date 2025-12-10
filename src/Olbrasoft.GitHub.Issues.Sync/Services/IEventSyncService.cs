using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Sync.Services;

/// <summary>
/// Service for synchronizing issue events from GitHub.
/// </summary>
public interface IEventSyncService
{
    /// <summary>
    /// Synchronizes issue events from GitHub for a repository.
    /// </summary>
    Task SyncEventsAsync(
        Repository repository,
        string owner,
        string repo,
        DateTimeOffset? since = null,
        CancellationToken cancellationToken = default);
}
