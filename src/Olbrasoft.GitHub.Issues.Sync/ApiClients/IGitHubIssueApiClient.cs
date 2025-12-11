namespace Olbrasoft.GitHub.Issues.Sync.ApiClients;

/// <summary>
/// Client for fetching issues from GitHub REST API.
/// Single responsibility: HTTP communication with GitHub API.
/// </summary>
public interface IGitHubIssueApiClient
{
    /// <summary>
    /// Fetches all issues from a repository with pagination.
    /// </summary>
    /// <param name="owner">Repository owner</param>
    /// <param name="repo">Repository name</param>
    /// <param name="since">Optional: only fetch issues updated since this date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of issue DTOs</returns>
    Task<IReadOnlyList<GitHubIssueDto>> FetchIssuesAsync(
        string owner,
        string repo,
        DateTimeOffset? since = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// DTO representing a GitHub issue from the API.
/// </summary>
public record GitHubIssueDto(
    int Number,
    string Title,
    string? Body,
    string State,
    string HtmlUrl,
    DateTimeOffset UpdatedAt,
    string? ParentIssueUrl,
    IReadOnlyList<string> LabelNames,
    bool IsPullRequest);
