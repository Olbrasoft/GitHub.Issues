namespace Olbrasoft.GitHub.Issues.Sync.Webhooks;

/// <summary>
/// Configuration settings for GitHub webhooks.
/// </summary>
public class WebhookSettings
{
    /// <summary>
    /// GitHub webhook secret for HMAC-SHA256 signature validation.
    /// If not set, signature validation is disabled (development mode only).
    /// </summary>
    public string? WebhookSecret { get; set; }
}
