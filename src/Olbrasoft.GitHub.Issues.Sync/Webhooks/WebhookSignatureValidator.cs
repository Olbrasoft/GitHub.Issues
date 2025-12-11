using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Olbrasoft.GitHub.Issues.Sync.Webhooks;

/// <summary>
/// Validates GitHub webhook signatures using HMAC-SHA256.
/// </summary>
public class WebhookSignatureValidator : IWebhookSignatureValidator
{
    private readonly string? _webhookSecret;
    private readonly ILogger<WebhookSignatureValidator> _logger;

    public WebhookSignatureValidator(
        IOptions<WebhookSettings> settings,
        ILogger<WebhookSignatureValidator> logger)
    {
        _webhookSecret = settings.Value.WebhookSecret;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool ValidateSignature(byte[] payload, string? signatureHeader)
    {
        // If no secret configured, skip validation (development mode)
        if (string.IsNullOrEmpty(_webhookSecret))
        {
            _logger.LogWarning("Webhook secret not configured - signature validation disabled");
            return true;
        }

        if (string.IsNullOrEmpty(signatureHeader))
        {
            _logger.LogWarning("Missing X-Hub-Signature-256 header");
            return false;
        }

        // GitHub signature format: "sha256=<hex>"
        if (!signatureHeader.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Invalid signature format - expected sha256= prefix");
            return false;
        }

        var expectedSignature = signatureHeader[7..]; // Remove "sha256=" prefix

        // Calculate HMAC-SHA256
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_webhookSecret));
        var hash = hmac.ComputeHash(payload);
        var actualSignature = Convert.ToHexString(hash).ToLowerInvariant();

        // Constant-time comparison to prevent timing attacks
        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedSignature.ToLowerInvariant()),
            Encoding.UTF8.GetBytes(actualSignature)))
        {
            _logger.LogWarning("Webhook signature mismatch");
            return false;
        }

        _logger.LogDebug("Webhook signature validated successfully");
        return true;
    }
}
