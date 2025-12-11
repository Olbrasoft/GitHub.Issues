namespace Olbrasoft.GitHub.Issues.Business;

/// <summary>
/// DTO for summary notification sent via SignalR.
/// </summary>
public record SummaryNotificationDto(
    int IssueId,
    string Summary,
    string Provider);

/// <summary>
/// Interface for notifying clients when AI summary is ready.
/// </summary>
public interface ISummaryNotifier
{
    /// <summary>
    /// Sends summary to subscribed clients via SignalR.
    /// </summary>
    Task NotifySummaryReadyAsync(SummaryNotificationDto notification, CancellationToken cancellationToken = default);
}

/// <summary>
/// Null implementation for when SignalR is not configured.
/// </summary>
public class NullSummaryNotifier : ISummaryNotifier
{
    public Task NotifySummaryReadyAsync(SummaryNotificationDto notification, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
