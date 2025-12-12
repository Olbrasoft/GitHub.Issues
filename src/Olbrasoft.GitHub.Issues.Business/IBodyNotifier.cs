namespace Olbrasoft.GitHub.Issues.Business;

/// <summary>
/// DTO for body notification sent via SignalR.
/// </summary>
/// <param name="IssueId">Database issue ID</param>
/// <param name="BodyPreview">First N characters of issue body</param>
public record BodyNotificationDto(
    int IssueId,
    string BodyPreview);

/// <summary>
/// Interface for notifying clients when issue body is fetched.
/// </summary>
public interface IBodyNotifier
{
    /// <summary>
    /// Sends body preview to subscribed clients via SignalR.
    /// </summary>
    Task NotifyBodyReceivedAsync(BodyNotificationDto notification, CancellationToken cancellationToken = default);
}

/// <summary>
/// Null implementation for when SignalR is not configured.
/// </summary>
public class NullBodyNotifier : IBodyNotifier
{
    public Task NotifyBodyReceivedAsync(BodyNotificationDto notification, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
