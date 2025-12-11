namespace Olbrasoft.GitHub.Issues.Sync.ApiClients;

/// <summary>
/// Client for fetching repositories from GitHub REST API.
/// Single responsibility: HTTP communication with GitHub API for repositories.
/// </summary>
public interface IGitHubRepositoryApiClient
{
    /// <summary>
    /// Fetches all repositories for an owner (user or organization).
    /// </summary>
    /// <param name="owner">Repository owner (user or org name)</param>
    /// <param name="ownerType">Type of owner: "org" or "user"</param>
    /// <param name="includeArchived">Include archived repositories</param>
    /// <param name="includeForks">Include forked repositories</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of repository full names (e.g., "owner/repo")</returns>
    Task<IReadOnlyList<string>> FetchRepositoriesForOwnerAsync(
        string owner,
        string ownerType,
        bool includeArchived,
        bool includeForks,
        CancellationToken cancellationToken = default);
}
