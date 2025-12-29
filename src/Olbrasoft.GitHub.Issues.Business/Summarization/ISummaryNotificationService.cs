namespace Olbrasoft.GitHub.Issues.Business.Summarization;

/// <summary>
/// Service for notifying clients about summary generation.
/// Single responsibility: Send notifications (SRP).
/// Wraps ISummaryNotifier to provide cleaner API for business layer.
/// </summary>
public interface ISummaryNotificationService
{
    /// <summary>
    /// Notifies clients that summary is ready.
    /// </summary>
    /// <param name="issueId">Issue ID</param>
    /// <param name="summary">Generated summary text</param>
    /// <param name="provider">Provider info (e.g., "Cerebras/llama3.1-8b")</param>
    /// <param name="language">Language code ("en" or "cs")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task NotifySummaryAsync(
        int issueId,
        string summary,
        string provider,
        string language,
        CancellationToken cancellationToken = default);
}
