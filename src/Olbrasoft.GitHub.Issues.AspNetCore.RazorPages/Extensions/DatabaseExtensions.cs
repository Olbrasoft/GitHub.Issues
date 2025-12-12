using Microsoft.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;

namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Extensions;

/// <summary>
/// Extension methods for configuring database services.
/// </summary>
public static class DatabaseExtensions
{
    /// <summary>
    /// Applies pending migrations to the database on startup.
    /// </summary>
    public static async Task ApplyMigrationsAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GitHubDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<GitHubDbContext>>();

        try
        {
            var pendingMigrations = await db.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                logger.LogInformation("Applying {Count} pending migrations: {Migrations}",
                    pendingMigrations.Count(), string.Join(", ", pendingMigrations));
                await db.Database.MigrateAsync();
                logger.LogInformation("Migrations applied successfully");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to apply database migrations");
            throw;
        }
    }

    public static IServiceCollection AddGitHubDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var databaseSettings = configuration.GetSection("Database").Get<DatabaseSettings>()
            ?? new DatabaseSettings { Provider = DatabaseProvider.PostgreSQL };

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        var dbPassword = configuration["DbPassword"];

        if (!string.IsNullOrEmpty(dbPassword))
        {
            connectionString += $";Password={dbPassword}";
        }

        services.AddGitHubDbContext(connectionString!, databaseSettings.Provider);
        services.Configure<DatabaseSettings>(configuration.GetSection("Database"));

        return services;
    }
}
