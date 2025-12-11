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
