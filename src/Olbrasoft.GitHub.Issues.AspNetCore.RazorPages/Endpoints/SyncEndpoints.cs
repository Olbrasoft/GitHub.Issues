using System.Text.Json;
using Olbrasoft.GitHub.Issues.Business;
using Olbrasoft.GitHub.Issues.Sync.Services;

namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Endpoints;

/// <summary>
/// Data synchronization API endpoints.
/// </summary>
public static class SyncEndpoints
{
    public static WebApplication MapSyncEndpoints(this WebApplication app)
    {
        app.MapPost("/api/data/import", async (
            IGitHubSyncService syncService,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            try
            {
                logger.LogInformation("Starting data import from GitHub (smart sync mode)");
                await syncService.SyncAllRepositoriesAsync(since: null, smartMode: true, generateEmbeddings: true, cancellationToken: ct);

                return Results.Ok(new { success = true, message = "Import dokončen" });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Import failed");
                return Results.BadRequest(new { success = false, message = ex.Message });
            }
        }).RequireAuthorization("OwnerOnly");

        app.MapPost("/api/data/sync/analyze", async (
            HttpRequest request,
            IGitHubSyncService syncService,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            try
            {
                List<string>? repositoryFullNames = null;
                bool fullRefresh = false;

                if (request.ContentLength > 0)
                {
                    using var reader = new StreamReader(request.Body);
                    var body = await reader.ReadToEndAsync(ct);
                    if (!string.IsNullOrWhiteSpace(body))
                    {
                        var json = JsonDocument.Parse(body);

                        if (json.RootElement.TryGetProperty("repositoryFullNames", out var reposElement) &&
                            reposElement.ValueKind == JsonValueKind.Array)
                        {
                            repositoryFullNames = reposElement.EnumerateArray()
                                .Select(e => e.GetString())
                                .Where(s => !string.IsNullOrWhiteSpace(s))
                                .Cast<string>()
                                .ToList();
                        }
                        else if (json.RootElement.TryGetProperty("repositoryFullName", out var repoElement))
                        {
                            var singleRepo = repoElement.GetString();
                            if (!string.IsNullOrWhiteSpace(singleRepo))
                            {
                                repositoryFullNames = [singleRepo];
                            }
                        }

                        if (json.RootElement.TryGetProperty("fullRefresh", out var refreshElement))
                        {
                            fullRefresh = refreshElement.GetBoolean();
                        }
                    }
                }

                var smartMode = !fullRefresh;

                if (repositoryFullNames != null && repositoryFullNames.Count > 0)
                {
                    // Analyze specific repositories
                    var analysisResults = new List<object>();

                    foreach (var repoFullName in repositoryFullNames)
                    {
                        var parts = repoFullName.Split('/');
                        if (parts.Length != 2)
                        {
                            return Results.BadRequest(new { success = false, message = $"Invalid repository format: {repoFullName}. Expected: owner/repo" });
                        }

                        var analysis = await syncService.AnalyzeRepositoryAsync(parts[0], parts[1], since: null, smartMode: smartMode, ct);
                        analysisResults.Add(new
                        {
                            repository = analysis.RepositoryFullName,
                            totalIssues = analysis.TotalIssues,
                            newIssues = analysis.NewIssues,
                            changedIssues = analysis.ChangedIssues,
                            missingEmbeddings = analysis.MissingEmbeddings,
                            requiredCohereApiCalls = analysis.RequiredCohereApiCalls
                        });
                    }

                    var totalNewOrChanged = analysisResults.Sum(a => ((dynamic)a).newIssues + ((dynamic)a).changedIssues);

                    return Results.Ok(new
                    {
                        success = true,
                        repositories = analysisResults,
                        summary = new
                        {
                            totalRepositories = analysisResults.Count,
                            totalNewOrChanged = totalNewOrChanged,
                            totalCohereApiCalls = totalNewOrChanged
                        }
                    });
                }
                else
                {
                    return Results.BadRequest(new { success = false, message = "No repositories specified for analysis. Provide repositoryFullNames or repositoryFullName." });
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Sync analysis failed");
                return Results.BadRequest(new { success = false, message = ex.Message });
            }
        }).RequireAuthorization("OwnerOnly");

        app.MapPost("/api/data/sync", async (
            HttpRequest request,
            IGitHubSyncService syncService,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            try
            {
                List<string>? repositoryFullNames = null;
                bool fullRefresh = false;
                bool generateEmbeddings = true; // Default: generate embeddings

                if (request.ContentLength > 0)
                {
                    using var reader = new StreamReader(request.Body);
                    var body = await reader.ReadToEndAsync(ct);
                    if (!string.IsNullOrWhiteSpace(body))
                    {
                        var json = JsonDocument.Parse(body);

                        if (json.RootElement.TryGetProperty("repositoryFullNames", out var reposElement) &&
                            reposElement.ValueKind == JsonValueKind.Array)
                        {
                            repositoryFullNames = reposElement.EnumerateArray()
                                .Select(e => e.GetString())
                                .Where(s => !string.IsNullOrWhiteSpace(s))
                                .Cast<string>()
                                .ToList();
                        }
                        else if (json.RootElement.TryGetProperty("repositoryFullName", out var repoElement))
                        {
                            var singleRepo = repoElement.GetString();
                            if (!string.IsNullOrWhiteSpace(singleRepo))
                            {
                                repositoryFullNames = [singleRepo];
                            }
                        }

                        if (json.RootElement.TryGetProperty("fullRefresh", out var refreshElement))
                        {
                            fullRefresh = refreshElement.GetBoolean();
                        }

                        // Parse generateEmbeddings parameter (default: true)
                        if (json.RootElement.TryGetProperty("generateEmbeddings", out var embeddingsElement))
                        {
                            generateEmbeddings = embeddingsElement.GetBoolean();
                        }
                    }
                }

                var smartMode = !fullRefresh;
                Olbrasoft.GitHub.Issues.Data.Dtos.SyncStatisticsDto stats;

                if (repositoryFullNames != null && repositoryFullNames.Count > 0)
                {
                    foreach (var repoFullName in repositoryFullNames)
                    {
                        var parts = repoFullName.Split('/');
                        if (parts.Length != 2)
                        {
                            return Results.BadRequest(new { success = false, message = $"Invalid repository format: {repoFullName}. Expected: owner/repo" });
                        }
                    }

                    logger.LogInformation("Starting sync for {Count} repositories (smartMode: {SmartMode}, generateEmbeddings: {GenerateEmbeddings})",
                        repositoryFullNames.Count, smartMode, generateEmbeddings);
                    stats = await syncService.SyncRepositoriesAsync(repositoryFullNames, since: null, smartMode: smartMode, generateEmbeddings: generateEmbeddings, ct);

                    var repoLabel = repositoryFullNames.Count == 1 ? repositoryFullNames[0] : $"{repositoryFullNames.Count} repozitářů";
                    return Results.Ok(new
                    {
                        success = true,
                        message = $"Synchronizace {repoLabel} dokončena",
                        statistics = CreateStatisticsResponse(stats)
                    });
                }
                else
                {
                    logger.LogInformation("Starting sync for all repositories (smartMode: {SmartMode}, generateEmbeddings: {GenerateEmbeddings})", smartMode, generateEmbeddings);
                    stats = await syncService.SyncAllRepositoriesAsync(since: null, smartMode: smartMode, generateEmbeddings: generateEmbeddings, ct);

                    return Results.Ok(new
                    {
                        success = true,
                        message = "Synchronizace všech repozitářů dokončena",
                        statistics = CreateStatisticsResponse(stats)
                    });
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Sync failed");
                var errorMessage = ex.Message;
                if (ex.InnerException != null)
                {
                    errorMessage += $" Inner: {ex.InnerException.Message}";
                    if (ex.InnerException.InnerException != null)
                    {
                        errorMessage += $" Inner2: {ex.InnerException.InnerException.Message}";
                    }
                }
                return Results.BadRequest(new { success = false, message = errorMessage });
            }
        }).RequireAuthorization("OwnerOnly");

        app.MapPost("/api/data/reset-sync", async (
            HttpRequest request,
            IRepositorySyncBusinessService repositoryBusiness,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            try
            {
                string? fullName = null;

                if (request.ContentLength > 0)
                {
                    using var reader = new StreamReader(request.Body);
                    var body = await reader.ReadToEndAsync(ct);
                    if (!string.IsNullOrWhiteSpace(body))
                    {
                        var json = JsonDocument.Parse(body);
                        if (json.RootElement.TryGetProperty("repositoryFullName", out var repoElement))
                        {
                            fullName = repoElement.GetString();
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(fullName))
                {
                    return Results.BadRequest(new { success = false, message = "Repository full name is required" });
                }

                logger.LogInformation("Resetting LastSyncedAt for repository: {FullName}", fullName);
                var success = await repositoryBusiness.ResetLastSyncedAsync(fullName, ct);

                if (success)
                {
                    return Results.Ok(new { success = true, message = $"LastSyncedAt reset for {fullName}" });
                }
                else
                {
                    return Results.NotFound(new { success = false, message = $"Repository {fullName} not found" });
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Reset sync failed");
                return Results.BadRequest(new { success = false, message = ex.Message });
            }
        }).RequireAuthorization("OwnerOnly");

        return app;
    }

    private static object CreateStatisticsResponse(Olbrasoft.GitHub.Issues.Data.Dtos.SyncStatisticsDto stats) => new
    {
        apiCalls = stats.ApiCalls,
        totalFound = stats.TotalFound,
        created = stats.Created,
        updated = stats.Updated,
        unchanged = stats.Unchanged,
        embeddingsFailed = stats.EmbeddingsFailed,
        sinceTimestamp = stats.SinceTimestamp
    };
}
