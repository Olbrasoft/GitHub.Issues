namespace Olbrasoft.GitHub.Issues.Business;

/// <summary>
/// DTO for summary notification sent via SignalR.
/// </summary>
/// <param name="IssueId">Database issue ID</param>
/// <param name="Summary">AI-generated summary text</param>
/// <param name="Provider">Provider/model info (e.g., "Cerebras/llama-4 → Cohere/aya")</param>
/// <param name="Language">Language code: "en" or "cs"</param>
public record SummaryNotificationDto(
    int IssueId,
    string Summary,
    string Provider,
    string Language = "cs");

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
