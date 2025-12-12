using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;

namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Extensions;

/// <summary>
/// Extension methods for configuring database services.
/// </summary>
public static class DatabaseExtensions
{
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
