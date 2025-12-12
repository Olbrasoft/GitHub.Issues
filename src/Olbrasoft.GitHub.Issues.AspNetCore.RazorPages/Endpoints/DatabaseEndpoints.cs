using Microsoft.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Business;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;

namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Endpoints;

/// <summary>
/// Database management API endpoints.
/// </summary>
public static class DatabaseEndpoints
{
    public static WebApplication MapDatabaseEndpoints(this WebApplication app)
    {
        app.MapGet("/api/database/status", async (IDatabaseStatusService dbStatus, CancellationToken ct) =>
        {
            var status = await dbStatus.GetStatusAsync(ct);
            return Results.Ok(status);
        });

        app.MapPost("/api/database/migrate", async (IDatabaseStatusService dbStatus, CancellationToken ct) =>
        {
            var result = await dbStatus.ApplyMigrationsAsync(ct);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        }).RequireAuthorization("OwnerOnly");

        // Diagnostic endpoint: Test embedding service with detailed Cohere API response
        app.MapGet("/api/database/test-embedding", async (
            IConfiguration config,
            IHttpClientFactory httpClientFactory,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var testText = "This is a test issue for embedding generation";
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Get API keys from config
            var apiKey1 = config["Embedding:CohereApiKey"];
            var apiKey2 = config["Embedding:CohereApiKey2"];
            var keys = new List<(string key, string name)>();
            if (!string.IsNullOrEmpty(apiKey1)) keys.Add((apiKey1, "CohereApiKey"));
            if (!string.IsNullOrEmpty(apiKey2)) keys.Add((apiKey2, "CohereApiKey2"));

            if (keys.Count == 0)
            {
                return Results.Ok(new { success = false, error = "No Cohere API keys configured" });
            }

            var results = new List<object>();
            var httpClient = httpClientFactory.CreateClient();

            foreach (var (key, name) in keys)
            {
                var keyResult = await TestCohereKeyAsync(httpClient, key, name, testText, ct);
                results.Add(keyResult);
            }

            stopwatch.Stop();
            return Results.Ok(new
            {
                totalLatencyMs = stopwatch.ElapsedMilliseconds,
                keysConfigured = keys.Count,
                keyResults = results
            });
        });

        // Diagnostic endpoint: Test embedding service (uses DI)

        // Diagnostic endpoint: Find issues without embeddings
        app.MapGet("/api/database/issues-without-embeddings", async (
            GitHubDbContext db,
            CancellationToken ct) =>
        {
            var issuesWithoutEmbedding = await db.Issues
                .Where(i => i.Embedding == null)
                .Include(i => i.Repository)
                .Select(i => new
                {
                    i.Id,
                    i.Number,
                    i.Title,
                    TitleLength = i.Title.Length,
                    Repository = i.Repository.FullName,
                    i.Url,
                    i.IsOpen,
                    i.SyncedAt
                })
                .OrderByDescending(i => i.SyncedAt)
                .Take(100)
                .ToListAsync(ct);

            var totalWithoutEmbedding = await db.Issues.CountAsync(i => i.Embedding == null, ct);
            var totalIssues = await db.Issues.CountAsync(ct);

            return Results.Ok(new
            {
                totalIssues,
                totalWithoutEmbedding,
                percentageWithoutEmbedding = totalIssues > 0 ? Math.Round(100.0 * totalWithoutEmbedding / totalIssues, 2) : 0,
                issues = issuesWithoutEmbedding
            });
        });

        return app;
    }

    private static async Task<object> TestCohereKeyAsync(
        HttpClient httpClient,
        string apiKey,
        string keyName,
        string testText,
        CancellationToken ct)
    {
        var maskedKey = apiKey.Length > 4 ? $"...{apiKey[^4..]}" : "****";
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var request = new
            {
                texts = new[] { testText },
                model = "embed-multilingual-v3.0",
                input_type = "search_document",
                embedding_types = new[] { "float" }
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.cohere.com/v2/embed");
            httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            httpRequest.Content = System.Net.Http.Json.JsonContent.Create(request);

            var response = await httpClient.SendAsync(httpRequest, ct);
            stopwatch.Stop();

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            var statusCode = (int)response.StatusCode;

            return new
            {
                keyName,
                maskedKey,
                statusCode,
                success = response.IsSuccessStatusCode,
                latencyMs = stopwatch.ElapsedMilliseconds,
                response = statusCode != 200 ? responseBody : "(embedding generated successfully)",
                embeddingGenerated = response.IsSuccessStatusCode
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new
            {
                keyName,
                maskedKey,
                statusCode = 0,
                success = false,
                latencyMs = stopwatch.ElapsedMilliseconds,
                error = ex.Message
            };
        }
    }
}
