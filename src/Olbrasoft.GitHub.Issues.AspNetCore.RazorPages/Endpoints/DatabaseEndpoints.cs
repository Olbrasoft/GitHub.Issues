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

        // Diagnostic endpoint: Test embedding service
        app.MapGet("/api/database/test-embedding", async (
            Olbrasoft.GitHub.Issues.Text.Transformation.Abstractions.IEmbeddingService embeddingService,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var testText = "This is a test issue for embedding generation";
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var embedding = await embeddingService.GenerateEmbeddingAsync(testText,
                    Olbrasoft.GitHub.Issues.Text.Transformation.Abstractions.EmbeddingInputType.Document, ct);
                stopwatch.Stop();

                return Results.Ok(new
                {
                    success = embedding != null,
                    isConfigured = embeddingService.IsConfigured,
                    embeddingLength = embedding?.Length ?? 0,
                    latencyMs = stopwatch.ElapsedMilliseconds,
                    testText = testText
                });
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                logger.LogError(ex, "Embedding test failed");
                return Results.Ok(new
                {
                    success = false,
                    isConfigured = embeddingService.IsConfigured,
                    error = ex.Message,
                    innerError = ex.InnerException?.Message,
                    latencyMs = stopwatch.ElapsedMilliseconds
                });
            }
        });

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
}
