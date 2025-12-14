using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Business;
using Olbrasoft.GitHub.Issues.Business.Services;
using Olbrasoft.GitHub.Issues.Data.Commands.IssueCommands;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.Queries.IssueQueries;
using Olbrasoft.GitHub.Issues.Sync.ApiClients;
using Olbrasoft.GitHub.Issues.Sync.Services;
using Olbrasoft.Mediation;

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

        // Translate issue titles to target language
        app.MapPost("/api/issues/translate-titles", async (
            HttpRequest request,
            IServiceScopeFactory scopeFactory,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            List<int> issueIds;
            string targetLanguage;

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

                // Parse target language (default to Czech for backward compatibility)
                targetLanguage = json.RootElement.TryGetProperty("targetLanguage", out var langElement)
                    ? langElement.GetString() ?? "cs"
                    : "cs";

                // Validate language code
                if (targetLanguage is not ("en" or "de" or "cs"))
                {
                    return Results.BadRequest(new { error = "Invalid targetLanguage. Use 'en', 'de', or 'cs'" });
                }
            }
            catch (JsonException)
            {
                return Results.BadRequest(new { error = "Invalid JSON" });
            }

            if (issueIds.Count == 0)
            {
                return Results.Ok(new { message = "No issues to translate" });
            }

            // Skip translation for English (titles are already in English)
            if (targetLanguage == "en")
            {
                logger.LogInformation("API: Skipping title translation - target language is English");
                return Results.Ok(new { message = "Skipped - titles are already in English" });
            }

            logger.LogInformation("API: Starting title translation for {Count} issues to {Lang}", issueIds.Count, targetLanguage);

            foreach (var issueId in issueIds)
            {
                var capturedId = issueId;
                var capturedLang = targetLanguage;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await using var scope = scopeFactory.CreateAsyncScope();
                        var translationService = scope.ServiceProvider.GetRequiredService<ITitleTranslationService>();
                        await translationService.TranslateTitleAsync(capturedId, capturedLang, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Background title translation failed for issue {Id}", capturedId);
                    }
                });
            }

            return Results.Accepted(value: new { message = $"Translating {issueIds.Count} titles to {targetLanguage}" });
        });

        // Fetch issue bodies from GitHub GraphQL and generate AI summaries (for progressive loading)
        app.MapPost("/api/issues/fetch-bodies", async (
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

                // Parse language preference (default to English)
                language = json.RootElement.TryGetProperty("language", out var langElement)
                    ? langElement.GetString() ?? "en"
                    : "en";

                // Map language to summary format
                // en -> en (English only)
                // cs/de/other -> both (English first, then translated)
                if (language != "en")
                {
                    language = "both"; // Generate both English and translated summary
                }
            }
            catch (JsonException)
            {
                return Results.BadRequest(new { error = "Invalid JSON" });
            }

            if (issueIds.Count == 0)
            {
                return Results.Ok(new { message = "No issues to fetch" });
            }

            logger.LogInformation("API: Starting body fetch and summarization for {Count} issues, language={Language}", issueIds.Count, language);

            var capturedLanguage = language;
            // Run body fetch and summarization in background (fire-and-forget)
            _ = Task.Run(async () =>
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var issueDetailService = scope.ServiceProvider.GetRequiredService<IIssueDetailService>();
                    await issueDetailService.FetchBodiesAsync(issueIds, capturedLanguage, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Background body fetch failed for issues");
                }
            });

            return Results.Accepted(value: new { message = $"Fetching bodies and generating summaries for {issueIds.Count} issues" });
        });

        // Change issue state (open/close) on GitHub
        app.MapPost("/api/issues/{id:int}/change-state", async (
            int id,
            HttpRequest request,
            GitHubDbContext dbContext,
            IGitHubApiClient gitHubClient,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            // Parse request body
            string state;
            try
            {
                using var reader = new StreamReader(request.Body);
                var body = await reader.ReadToEndAsync(ct);
                var json = JsonDocument.Parse(body);

                if (!json.RootElement.TryGetProperty("state", out var stateElement))
                {
                    return Results.BadRequest(new { error = "Missing 'state' property" });
                }

                state = stateElement.GetString() ?? "";
                if (state is not ("open" or "closed"))
                {
                    return Results.BadRequest(new { error = "State must be 'open' or 'closed'" });
                }
            }
            catch (JsonException)
            {
                return Results.BadRequest(new { error = "Invalid JSON" });
            }

            // Get issue from database
            var issue = await dbContext.Issues
                .Include(i => i.Repository)
                .FirstOrDefaultAsync(i => i.Id == id, ct);

            if (issue == null)
            {
                return Results.NotFound(new { error = $"Issue with ID {id} not found" });
            }

            // Parse owner and repo from full name
            var parts = issue.Repository.FullName.Split('/');
            if (parts.Length != 2)
            {
                return Results.BadRequest(new { error = "Invalid repository format" });
            }

            var owner = parts[0];
            var repo = parts[1];

            try
            {
                // Update on GitHub
                logger.LogInformation("Changing issue state: {Owner}/{Repo}#{Number} -> {State}",
                    owner, repo, issue.Number, state);

                await gitHubClient.UpdateIssueStateAsync(owner, repo, issue.Number, state);

                // Update local database
                issue.IsOpen = state == "open";
                await dbContext.SaveChangesAsync(ct);

                logger.LogInformation("Issue state changed successfully: {Owner}/{Repo}#{Number} is now {State}",
                    owner, repo, issue.Number, state);

                return Results.Ok(new
                {
                    success = true,
                    id = issue.Id,
                    issueNumber = issue.Number,
                    isOpen = issue.IsOpen,
                    state = state,
                    message = state == "open" ? "Issue reopened" : "Issue closed"
                });
            }
            catch (Octokit.ApiException ex)
            {
                logger.LogError(ex, "GitHub API error while changing issue state");
                return Results.BadRequest(new { error = $"GitHub API error: {ex.Message}" });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error changing issue state");
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization("OwnerOnly");

        // Get Czech summaries for issues (for VirtualAssistant integration)
        // If issue not in DB, fetches from GitHub, syncs, and generates summary
        app.MapPost("/api/issues/summaries", async (
            HttpRequest request,
            GitHubDbContext dbContext,
            IMediator mediator,
            ITranslatedTextService translatedTextService,
            IGitHubIssueApiClient issueApiClient,
            IIssueEmbeddingGenerator embeddingGenerator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            List<int> issueNumbers;
            string owner;
            string repo;

            try
            {
                using var reader = new StreamReader(request.Body);
                var body = await reader.ReadToEndAsync(ct);
                var json = JsonDocument.Parse(body);

                if (!json.RootElement.TryGetProperty("issueNumbers", out var numbersElement) ||
                    numbersElement.ValueKind != JsonValueKind.Array)
                {
                    return Results.BadRequest(new { error = "Missing issueNumbers array" });
                }

                issueNumbers = numbersElement.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.Number)
                    .Select(e => e.GetInt32())
                    .ToList();

                if (!json.RootElement.TryGetProperty("owner", out var ownerElement) ||
                    ownerElement.ValueKind != JsonValueKind.String)
                {
                    return Results.BadRequest(new { error = "Missing owner" });
                }
                owner = ownerElement.GetString()!;

                if (!json.RootElement.TryGetProperty("repo", out var repoElement) ||
                    repoElement.ValueKind != JsonValueKind.String)
                {
                    return Results.BadRequest(new { error = "Missing repo" });
                }
                repo = repoElement.GetString()!;
            }
            catch (JsonException)
            {
                return Results.BadRequest(new { error = "Invalid JSON" });
            }

            if (issueNumbers.Count == 0)
            {
                return Results.Ok(new { summaries = new Dictionary<int, IssueSummaryResponse>() });
            }

            logger.LogInformation("API: Getting Czech summaries for {Count} issues in {Owner}/{Repo}",
                issueNumbers.Count, owner, repo);

            // Czech language LCID
            const int czechLanguageId = 1029;

            // Find repository
            var repository = await dbContext.Repositories
                .FirstOrDefaultAsync(r => r.FullName == $"{owner}/{repo}", ct);

            if (repository == null)
            {
                return Results.BadRequest(new { error = $"Repository {owner}/{repo} not found in database" });
            }

            // Look up existing issues by number
            var existingQuery = new IssuesByNumbersQuery(mediator)
            {
                IssueNumbers = issueNumbers,
                RepositoryIds = new[] { repository.Id }
            };
            var existingIssues = await existingQuery.ToResultAsync(ct);

            var foundNumbers = existingIssues.Select(i => i.IssueNumber).ToHashSet();
            var missingNumbers = issueNumbers.Where(n => !foundNumbers.Contains(n)).ToList();

            logger.LogInformation("Found {Found} issues in DB, {Missing} missing",
                foundNumbers.Count, missingNumbers.Count);

            // Fetch and sync missing issues from GitHub
            var newlySyncedIssueIds = new List<int>();
            foreach (var missingNumber in missingNumbers)
            {
                try
                {
                    var ghIssue = await issueApiClient.FetchIssueAsync(owner, repo, missingNumber, ct);
                    if (ghIssue == null)
                    {
                        logger.LogWarning("Issue #{Number} not found on GitHub", missingNumber);
                        continue;
                    }

                    if (ghIssue.IsPullRequest)
                    {
                        logger.LogDebug("Skipping PR #{Number}", missingNumber);
                        continue;
                    }

                    // Generate embedding
                    var embedding = await embeddingGenerator.GenerateEmbeddingAsync(
                        owner, repo, missingNumber, ghIssue.Title, ghIssue.Body, ghIssue.LabelNames, ct);

                    if (embedding == null)
                    {
                        logger.LogWarning("Failed to generate embedding for issue #{Number}", missingNumber);
                        continue;
                    }

                    // Save issue
                    var saveCommand = new IssueSaveCommand(mediator)
                    {
                        RepositoryId = repository.Id,
                        Number = ghIssue.Number,
                        Title = ghIssue.Title,
                        IsOpen = ghIssue.State == "open",
                        Url = ghIssue.HtmlUrl,
                        GitHubUpdatedAt = ghIssue.UpdatedAt,
                        SyncedAt = DateTimeOffset.UtcNow,
                        Embedding = embedding
                    };
                    var savedIssue = await saveCommand.ToResultAsync(ct);
                    newlySyncedIssueIds.Add(savedIssue.Id);

                    logger.LogInformation("Synced issue #{Number} (ID: {Id})", missingNumber, savedIssue.Id);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to sync issue #{Number}", missingNumber);
                }
            }

            // Get all issue IDs (existing + newly synced)
            var allIssueIds = existingIssues.Select(i => i.Id).Concat(newlySyncedIssueIds).ToList();

            // Get Czech summaries for all issues
            var summaries = await translatedTextService.GetForListAsync(allIssueIds, czechLanguageId, ct);

            // Build response mapping issue numbers to summaries
            var response = new Dictionary<int, IssueSummaryResponse>();

            foreach (var existing in existingIssues)
            {
                if (summaries.TryGetValue(existing.Id, out var summary))
                {
                    response[existing.IssueNumber] = new IssueSummaryResponse(
                        existing.IssueNumber,
                        existing.Title,
                        summary.Title,
                        summary.Summary,
                        existing.IsOpen,
                        existing.Url);
                }
            }

            // Add newly synced issues
            if (newlySyncedIssueIds.Count > 0)
            {
                var newIssues = await dbContext.Issues
                    .Where(i => newlySyncedIssueIds.Contains(i.Id))
                    .ToListAsync(ct);

                foreach (var issue in newIssues)
                {
                    if (summaries.TryGetValue(issue.Id, out var summary))
                    {
                        response[issue.Number] = new IssueSummaryResponse(
                            issue.Number,
                            issue.Title,
                            summary.Title,
                            summary.Summary,
                            issue.IsOpen,
                            issue.Url);
                    }
                }
            }

            logger.LogInformation("Returning {Count} Czech summaries", response.Count);

            return Results.Ok(new
            {
                summaries = response,
                syncedFromGitHub = missingNumbers.Where(n => response.ContainsKey(n)).ToList(),
                notFound = missingNumbers.Where(n => !response.ContainsKey(n)).ToList()
            });
        });

        return app;
    }
}

/// <summary>
/// Response DTO for issue summary.
/// </summary>
public record IssueSummaryResponse(
    int IssueNumber,
    string OriginalTitle,
    string CzechTitle,
    string CzechSummary,
    bool IsOpen,
    string Url);
