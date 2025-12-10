namespace Olbrasoft.GitHub.Issues.Sync.Services;

/// <summary>
/// Service for synchronizing repository information from GitHub.
/// </summary>
public interface IRepositorySyncService
{
    /// <summary>
    /// Ensures a repository exists in the database, creating it if necessary.
    /// </summary>
    Task<Data.Entities.Repository> EnsureRepositoryAsync(
        string owner,
        string repo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches all repositories for the configured owner using GitHub API.
    /// Filters based on IncludeArchived, IncludeForks, and has_issues settings.
    /// </summary>
    Task<List<string>> FetchAllRepositoriesForOwnerAsync(CancellationToken cancellationToken = default);
}
