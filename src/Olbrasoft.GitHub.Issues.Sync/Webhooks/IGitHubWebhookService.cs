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
    Task<WebhookProcessingResult> ProcessIssueEventAsync(
        GitHubIssueWebhookPayload payload,
        CancellationToken ct = default);

    /// <summary>
    /// Processes an issue_comment webhook event.
    /// Updates the comment count on the issue.
    /// </summary>
    Task<WebhookProcessingResult> ProcessIssueCommentEventAsync(
        GitHubIssueCommentWebhookPayload payload,
        CancellationToken ct = default);

    /// <summary>
    /// Processes a repository webhook event.
    /// Handles: created (auto-discovery of new repositories)
    /// </summary>
    Task<WebhookProcessingResult> ProcessRepositoryEventAsync(
        GitHubRepositoryWebhookPayload payload,
        CancellationToken ct = default);

    /// <summary>
    /// Processes a label webhook event.
    /// Handles: created, edited, deleted
    /// </summary>
    Task<WebhookProcessingResult> ProcessLabelEventAsync(
        GitHubLabelWebhookPayload payload,
        CancellationToken ct = default);
}
