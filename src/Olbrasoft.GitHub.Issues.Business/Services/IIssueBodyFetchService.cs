namespace Olbrasoft.GitHub.Issues.Business.Services;

/// <summary>
/// Service for fetching issue body content from GitHub GraphQL API.
/// Single responsibility: GitHub API interaction only (SRP).
/// </summary>
public interface IIssueBodyFetchService
{
    /// <summary>
    /// Fetches issue bodies from GitHub GraphQL API for multiple issues.
    /// Sends body previews via SignalR as they are fetched.
    /// </summary>
    /// <param name="issueIds">Issue IDs to fetch bodies for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task FetchBodiesAsync(IEnumerable<int> issueIds, CancellationToken cancellationToken = default);
}
