namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Services;

/// <summary>
/// Client for fetching issue bodies from GitHub GraphQL API.
/// </summary>
public interface IGitHubGraphQLClient
{
    /// <summary>
    /// Fetches bodies for multiple issues in a single GraphQL request.
    /// Issues are grouped by repository to minimize API calls.
    /// </summary>
    /// <param name="issues">List of issues with repository info and issue numbers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary mapping (owner, repo, issueNumber) to body text.</returns>
    Task<Dictionary<(string Owner, string Repo, int Number), string>> FetchBodiesAsync(
        IEnumerable<IssueBodyRequest> issues,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Request for fetching an issue body.
/// </summary>
public record IssueBodyRequest(string Owner, string Repo, int Number);
