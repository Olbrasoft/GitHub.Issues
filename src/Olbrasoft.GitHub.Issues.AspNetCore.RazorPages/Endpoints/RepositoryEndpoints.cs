using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Queries.RepositoryQueries;
using Olbrasoft.Mediation;

namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Endpoints;

/// <summary>
/// Repository-related API endpoints.
/// </summary>
public static class RepositoryEndpoints
{
    public static WebApplication MapRepositoryEndpoints(this WebApplication app)
    {
        app.MapGet("/api/repositories/search", async (string? term, IMediator mediator, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return Results.Ok(Array.Empty<object>());
            }

            var query = new RepositoriesSearchQuery(mediator)
            {
                Term = term,
                MaxResults = 15
            };

            var results = await query.ToResultAsync(ct);
            return Results.Ok(results);
        });

        app.MapGet("/api/repositories/sync-status", async (IMediator mediator, CancellationToken ct) =>
        {
            var query = new RepositoriesSyncStatusQuery(mediator);
            var results = await query.ToResultAsync(ct);
            return Results.Ok(results);
        });

        return app;
    }
}
