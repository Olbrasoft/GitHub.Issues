using System.Text.Json;
using Olbrasoft.GitHub.Issues.Sync.Webhooks;

namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Endpoints;

/// <summary>
/// GitHub webhook API endpoints.
/// </summary>
public static class WebhookEndpoints
{
    public static WebApplication MapWebhookEndpoints(this WebApplication app)
    {
        app.MapPost("/api/webhooks/github", async (
            HttpRequest request,
            IWebhookSignatureValidator signatureValidator,
            IGitHubWebhookService webhookService,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            // Read raw body for signature validation
            request.EnableBuffering();
            using var memoryStream = new MemoryStream();
            await request.Body.CopyToAsync(memoryStream, ct);
            var payload = memoryStream.ToArray();
            request.Body.Position = 0;

            // Validate signature
            var signature = request.Headers["X-Hub-Signature-256"].FirstOrDefault();
            if (!signatureValidator.ValidateSignature(payload, signature))
            {
                logger.LogWarning("Invalid webhook signature");
                return Results.Unauthorized();
            }

            // Check event type
            var eventType = request.Headers["X-GitHub-Event"].FirstOrDefault();
            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            try
            {
                WebhookProcessingResult result;

                switch (eventType)
                {
                    case "issues":
                        request.Body.Position = 0;
                        var issuePayload = await JsonSerializer.DeserializeAsync<GitHubIssueWebhookPayload>(
                            request.Body, jsonOptions, ct);
                        if (issuePayload == null)
                            return Results.BadRequest(new { error = "Empty payload" });
                        result = await webhookService.ProcessIssueEventAsync(issuePayload, ct);
                        break;

                    case "issue_comment":
                        request.Body.Position = 0;
                        var commentPayload = await JsonSerializer.DeserializeAsync<GitHubIssueCommentWebhookPayload>(
                            request.Body, jsonOptions, ct);
                        if (commentPayload == null)
                            return Results.BadRequest(new { error = "Empty payload" });
                        result = await webhookService.ProcessIssueCommentEventAsync(commentPayload, ct);
                        break;

                    case "repository":
                        request.Body.Position = 0;
                        var repoPayload = await JsonSerializer.DeserializeAsync<GitHubRepositoryWebhookPayload>(
                            request.Body, jsonOptions, ct);
                        if (repoPayload == null)
                            return Results.BadRequest(new { error = "Empty payload" });
                        result = await webhookService.ProcessRepositoryEventAsync(repoPayload, ct);
                        break;

                    case "label":
                        request.Body.Position = 0;
                        var labelPayload = await JsonSerializer.DeserializeAsync<GitHubLabelWebhookPayload>(
                            request.Body, jsonOptions, ct);
                        if (labelPayload == null)
                            return Results.BadRequest(new { error = "Empty payload" });
                        result = await webhookService.ProcessLabelEventAsync(labelPayload, ct);
                        break;

                    default:
                        logger.LogDebug("Ignoring webhook event type: {EventType}", eventType);
                        return Results.Ok(new { message = $"Ignored event: {eventType}" });
                }

                if (result.Success)
                {
                    logger.LogInformation("Webhook {Event} processed: {Message}", eventType, result.Message);
                    return Results.Ok(result);
                }
                else
                {
                    logger.LogWarning("Webhook {Event} failed: {Message}", eventType, result.Message);
                    return Results.BadRequest(result);
                }
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "Failed to parse webhook payload for event: {EventType}", eventType);
                return Results.BadRequest(new { error = "Invalid JSON payload" });
            }
        });

        return app;
    }
}
