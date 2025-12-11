namespace Olbrasoft.GitHub.Issues.Sync.Webhooks;

/// <summary>
/// Interface for validating GitHub webhook signatures.
/// </summary>
public interface IWebhookSignatureValidator
{
    /// <summary>
    /// Validates the HMAC-SHA256 signature of a GitHub webhook payload.
    /// </summary>
    /// <param name="payload">Raw request body bytes.</param>
    /// <param name="signatureHeader">The X-Hub-Signature-256 header value.</param>
    /// <returns>True if signature is valid, false otherwise.</returns>
    bool ValidateSignature(byte[] payload, string? signatureHeader);
}
