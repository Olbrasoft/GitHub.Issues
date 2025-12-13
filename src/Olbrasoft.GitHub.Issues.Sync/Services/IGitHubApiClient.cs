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

    /// <summary>
    /// Updates the state of an issue (open/closed) on GitHub.
    /// </summary>
    /// <param name="owner">Repository owner</param>
    /// <param name="repo">Repository name</param>
    /// <param name="issueNumber">Issue number</param>
    /// <param name="state">New state: "open" or "closed"</param>
    /// <returns>Updated issue from GitHub</returns>
    Task<Issue> UpdateIssueStateAsync(string owner, string repo, int issueNumber, string state);
}
