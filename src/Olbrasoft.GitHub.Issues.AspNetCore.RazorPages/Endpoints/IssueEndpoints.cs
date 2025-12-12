using System.Text.Json;
using Olbrasoft.GitHub.Issues.Business;

namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Endpoints;

/// <summary>
/// Issue-related API endpoints (summaries, translations).
/// </summary>
public static class IssueEndpoints
{
    public static WebApplication MapIssueEndpoints(this WebApplication app)
    {
        // Generate AI summary for single issue
        app.MapPost("/api/issues/{id:int}/generate-summary", (
            int id,
            HttpRequest request,
            IServiceScopeFactory scopeFactory,
            ILogger<Program> logger) =>
        {
            var language = request.Query["language"].FirstOrDefault() ?? "both";
            if (language is not ("en" or "cs" or "both"))
            {
                language = "both";
            }

            logger.LogInformation("API: Starting summary generation for issue {Id}, language={Language}", id, language);

            var capturedLanguage = language;
            _ = Task.Run(async () =>
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var issueDetailService = scope.ServiceProvider.GetRequiredService<IIssueDetailService>();
                    await issueDetailService.GenerateSummaryAsync(id, capturedLanguage, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Background summary generation failed for issue {Id}", id);
                }
            });

            return Results.Accepted();
        });

        // Generate AI summaries for multiple issues
        app.MapPost("/api/summaries/generate", async (
            HttpRequest request,
            IServiceScopeFactory scopeFactory,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            List<int> issueIds;
            string language;

            try
            {
                using var reader = new StreamReader(request.Body);
                var body = await reader.ReadToEndAsync(ct);
                var json = JsonDocument.Parse(body);

                if (!json.RootElement.TryGetProperty("issueIds", out var idsElement) ||
                    idsElement.ValueKind != JsonValueKind.Array)
                {
                    return Results.BadRequest(new { error = "Missing issueIds array" });
                }

                issueIds = idsElement.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.Number)
                    .Select(e => e.GetInt32())
                    .ToList();

                language = json.RootElement.TryGetProperty("language", out var langElement)
                    ? langElement.GetString() ?? "both"
                    : "both";

                if (language is not ("en" or "cs" or "both"))
                {
                    return Results.BadRequest(new { error = "Invalid language. Use 'en', 'cs', or 'both'" });
                }
            }
            catch (JsonException)
            {
                return Results.BadRequest(new { error = "Invalid JSON" });
            }

            if (issueIds.Count == 0)
            {
                return Results.Ok(new { message = "No issues to process" });
            }

            logger.LogInformation("API: Starting summary generation for {Count} issues, language={Language}",
                issueIds.Count, language);

            foreach (var issueId in issueIds)
            {
                var capturedId = issueId;
                var capturedLanguage = language;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await using var scope = scopeFactory.CreateAsyncScope();
                        var issueDetailService = scope.ServiceProvider.GetRequiredService<IIssueDetailService>();
                        await issueDetailService.GenerateSummaryAsync(capturedId, capturedLanguage, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Background summary generation failed for issue {Id}", capturedId);
                    }
                });
            }

            return Results.Accepted(value: new { message = $"Started summary generation for {issueIds.Count} issues", language });
        });

        // Translate issue titles to Czech
        app.MapPost("/api/issues/translate-titles", async (
            HttpRequest request,
            IServiceScopeFactory scopeFactory,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            List<int> issueIds;

            try
            {
                using var reader = new StreamReader(request.Body);
                var body = await reader.ReadToEndAsync(ct);
                var json = JsonDocument.Parse(body);

                if (!json.RootElement.TryGetProperty("issueIds", out var idsElement) ||
                    idsElement.ValueKind != JsonValueKind.Array)
                {
                    return Results.BadRequest(new { error = "Missing issueIds array" });
                }

                issueIds = idsElement.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.Number)
                    .Select(e => e.GetInt32())
                    .ToList();
            }
            catch (JsonException)
            {
                return Results.BadRequest(new { error = "Invalid JSON" });
            }

            if (issueIds.Count == 0)
            {
                return Results.Ok(new { message = "No issues to translate" });
            }

            logger.LogInformation("API: Starting title translation for {Count} issues", issueIds.Count);

            foreach (var issueId in issueIds)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await using var scope = scopeFactory.CreateAsyncScope();
                        var translationService = scope.ServiceProvider.GetRequiredService<ITitleTranslationService>();
                        await translationService.TranslateTitleAsync(issueId, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Background title translation failed for issue {Id}", issueId);
                    }
                });
            }

            return Results.Accepted(value: new { message = $"Translating {issueIds.Count} titles" });
        });

        return app;
    }
}
