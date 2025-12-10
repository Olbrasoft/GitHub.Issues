using Octokit;

namespace Olbrasoft.GitHub.Issues.Sync.Services;

/// <summary>
/// Abstraction over GitHub API client for testability.
/// </summary>
public interface IGitHubApiClient
{
    /// <summary>
    /// Gets repository information from GitHub.
    /// </summary>
    Task<Repository> GetRepositoryAsync(string owner, string repo);

    /// <summary>
    /// Gets all labels for a repository.
    /// </summary>
    Task<IReadOnlyList<Label>> GetLabelsForRepositoryAsync(string owner, string repo);
}
