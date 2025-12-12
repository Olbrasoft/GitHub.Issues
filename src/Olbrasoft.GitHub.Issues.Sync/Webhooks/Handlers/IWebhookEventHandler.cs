namespace Olbrasoft.GitHub.Issues.Sync.Webhooks.Handlers;

/// <summary>
/// Base interface for webhook event handlers.
/// Each handler is responsible for processing a specific type of GitHub webhook payload.
/// </summary>
/// <typeparam name="TPayload">The type of webhook payload this handler processes.</typeparam>
public interface IWebhookEventHandler<in TPayload>
{
    /// <summary>
    /// Processes the webhook payload and returns the result.
    /// </summary>
    /// <param name="payload">The webhook payload to process.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result of processing the webhook.</returns>
    Task<WebhookProcessingResult> HandleAsync(TPayload payload, CancellationToken ct = default);
}
