using Olbrasoft.GitHub.Issues.Business;

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

        return app;
    }
}
