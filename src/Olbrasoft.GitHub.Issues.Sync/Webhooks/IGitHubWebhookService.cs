namespace Olbrasoft.GitHub.Issues.Sync.Webhooks;

/// <summary>
/// Service for processing GitHub webhook events.
/// </summary>
public interface IGitHubWebhookService
{
    /// <summary>
    /// Processes an issue webhook event.
    /// Handles: opened, edited, closed, reopened, deleted, labeled, unlabeled
    /// </summary>
    /// <param name="payload">The webhook payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result of processing the webhook.</returns>
    Task<WebhookProcessingResult> ProcessIssueEventAsync(
        GitHubIssueWebhookPayload payload,
        CancellationToken ct = default);
}
