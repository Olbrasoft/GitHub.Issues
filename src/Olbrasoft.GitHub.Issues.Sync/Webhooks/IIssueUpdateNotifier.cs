namespace Olbrasoft.GitHub.Issues.Sync.Webhooks;

/// <summary>
/// DTO for issue update notifications sent via SignalR.
/// </summary>
public record IssueUpdateDto(
    int IssueId,
    int GitHubNumber,
    bool IsOpen,
    string Title,
    IReadOnlyList<LabelDto> Labels);

/// <summary>
/// DTO for label information in update notifications.
/// </summary>
public record LabelDto(string Name, string Color);

/// <summary>
/// Interface for notifying clients about issue updates in real-time.
/// </summary>
public interface IIssueUpdateNotifier
{
    /// <summary>
    /// Broadcasts an issue update to subscribed clients.
    /// </summary>
    /// <param name="update">The issue update data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task NotifyIssueUpdatedAsync(IssueUpdateDto update, CancellationToken cancellationToken = default);
}

/// <summary>
/// Null implementation of IIssueUpdateNotifier for when SignalR is not configured.
/// </summary>
public class NullIssueUpdateNotifier : IIssueUpdateNotifier
{
    public Task NotifyIssueUpdatedAsync(IssueUpdateDto update, CancellationToken cancellationToken = default)
    {
        // No-op - SignalR not configured
        return Task.CompletedTask;
    }
}

/// <summary>
/// DTO for new issue notifications to search result subscribers.
/// </summary>
public record NewIssueDto(
    int IssueId,
    int GitHubNumber,
    string RepositoryFullName,
    bool IsOpen,
    string Title,
    string Url,
    IReadOnlyList<LabelDto> Labels);

/// <summary>
/// Interface for notifying search result subscribers about new issues.
/// </summary>
public interface ISearchResultNotifier
{
    /// <summary>
    /// Broadcasts a notification about a new issue to all search result subscribers.
    /// </summary>
    /// <param name="newIssue">The new issue data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task NotifyNewIssueAsync(NewIssueDto newIssue, CancellationToken cancellationToken = default);
}

/// <summary>
/// Null implementation of ISearchResultNotifier for when SignalR is not configured.
/// </summary>
public class NullSearchResultNotifier : ISearchResultNotifier
{
    public Task NotifyNewIssueAsync(NewIssueDto newIssue, CancellationToken cancellationToken = default)
    {
        // No-op - SignalR not configured
        return Task.CompletedTask;
    }
}
