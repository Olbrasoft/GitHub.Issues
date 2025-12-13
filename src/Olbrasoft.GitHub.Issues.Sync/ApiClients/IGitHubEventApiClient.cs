namespace Olbrasoft.GitHub.Issues.Sync.ApiClients;

/// <summary>
/// Client for fetching issue events from GitHub REST API.
/// Single responsibility: HTTP communication with GitHub API for events.
/// </summary>
public interface IGitHubEventApiClient
{
    /// <summary>
    /// Fetches all issue events from a repository with pagination.
    /// </summary>
    /// <param name="owner">Repository owner</param>
    /// <param name="repo">Repository name</param>
    /// <param name="since">Optional: only fetch events created after this date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of event DTOs</returns>
    Task<IReadOnlyList<GitHubEventDto>> FetchEventsAsync(
        string owner,
        string repo,
        DateTimeOffset? since = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// DTO representing a GitHub issue event from the API.
/// </summary>
public record GitHubEventDto(
    long GitHubEventId,
    int IssueNumber,
    string EventType,
    DateTimeOffset CreatedAt);
