using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Business;

/// <summary>
/// Business service interface for repository sync operations.
/// </summary>
public interface IRepositorySyncBusinessService
{
    /// <summary>
    /// Gets a repository by its full name (owner/repo).
    /// </summary>
    Task<Repository?> GetByFullNameAsync(string fullName, CancellationToken ct = default);

    /// <summary>
    /// Saves (creates or updates) a repository.
    /// </summary>
    Task<Repository> SaveRepositoryAsync(long gitHubId, string fullName, string htmlUrl, CancellationToken ct = default);

    /// <summary>
    /// Updates the LastSyncedAt timestamp for a repository.
    /// </summary>
    Task<bool> UpdateLastSyncedAsync(int repositoryId, DateTimeOffset lastSyncedAt, CancellationToken ct = default);
}
