using Olbrasoft.GitHub.Issues.Sync.Webhooks.Handlers;

namespace Olbrasoft.GitHub.Issues.Sync.Webhooks;

/// <summary>
/// Service for processing GitHub webhook events.
/// Delegates to specialized handlers for each event type.
/// </summary>
public class GitHubWebhookService : IGitHubWebhookService
{
    private readonly IWebhookEventHandler<GitHubIssueWebhookPayload> _issueHandler;
    private readonly IWebhookEventHandler<GitHubIssueCommentWebhookPayload> _issueCommentHandler;
    private readonly IWebhookEventHandler<GitHubRepositoryWebhookPayload> _repositoryHandler;
    private readonly IWebhookEventHandler<GitHubLabelWebhookPayload> _labelHandler;

    public GitHubWebhookService(
        IWebhookEventHandler<GitHubIssueWebhookPayload> issueHandler,
        IWebhookEventHandler<GitHubIssueCommentWebhookPayload> issueCommentHandler,
        IWebhookEventHandler<GitHubRepositoryWebhookPayload> repositoryHandler,
        IWebhookEventHandler<GitHubLabelWebhookPayload> labelHandler)
    {
        _issueHandler = issueHandler;
        _issueCommentHandler = issueCommentHandler;
        _repositoryHandler = repositoryHandler;
        _labelHandler = labelHandler;
    }

    /// <inheritdoc />
    public Task<WebhookProcessingResult> ProcessIssueEventAsync(
        GitHubIssueWebhookPayload payload,
        CancellationToken ct = default)
        => _issueHandler.HandleAsync(payload, ct);

    /// <inheritdoc />
    public Task<WebhookProcessingResult> ProcessIssueCommentEventAsync(
        GitHubIssueCommentWebhookPayload payload,
        CancellationToken ct = default)
        => _issueCommentHandler.HandleAsync(payload, ct);

    /// <inheritdoc />
    public Task<WebhookProcessingResult> ProcessRepositoryEventAsync(
        GitHubRepositoryWebhookPayload payload,
        CancellationToken ct = default)
        => _repositoryHandler.HandleAsync(payload, ct);

    /// <inheritdoc />
    public Task<WebhookProcessingResult> ProcessLabelEventAsync(
        GitHubLabelWebhookPayload payload,
        CancellationToken ct = default)
        => _labelHandler.HandleAsync(payload, ct);
}
