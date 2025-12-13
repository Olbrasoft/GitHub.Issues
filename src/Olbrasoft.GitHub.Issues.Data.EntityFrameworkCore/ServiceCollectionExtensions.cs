using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Olbrasoft.Data.Cqrs;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;

/// <summary>
/// Extension methods for registering EF Core services with multi-provider support.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds GitHubDbContext with the specified database provider.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The database connection string.</param>
    /// <param name="provider">The database provider to use.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGitHubDbContext(
        this IServiceCollection services,
        string connectionString,
        DatabaseProvider provider)
    {
        // Register the settings as singleton for injection
        services.AddSingleton(new DatabaseSettings { Provider = provider });

        // Register DbContext with provider-specific options and migrations assembly
        services.AddDbContext<GitHubDbContext>((serviceProvider, options) =>
        {
            // Suppress warning about pending model changes (we manually edit migrations for safety)
            options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));

            switch (provider)
            {
                case DatabaseProvider.PostgreSQL:
                    options.UseNpgsql(connectionString, npgsqlOptions =>
                    {
                        npgsqlOptions.UseVector();
                        npgsqlOptions.MigrationsAssembly("Olbrasoft.GitHub.Issues.Migrations.PostgreSQL");
                    })
                    .UseSnakeCaseNamingConvention(); // PostgreSQL convention: snake_case
                    break;

                case DatabaseProvider.SqlServer:
                    options.UseSqlServer(connectionString, sqlOptions =>
                    {
                        sqlOptions.UseVectorSearch(); // Native VECTOR type support
                        sqlOptions.MigrationsAssembly("Olbrasoft.GitHub.Issues.Migrations.SqlServer");
                    });
                    // SQL Server: PascalCase (EF Core default, no convention needed)
                    break;

                default:
                    throw new ArgumentException($"Unsupported database provider: {provider}");
            }
        });

        // Register CQRS handlers from this assembly (replaces legacy IVectorSearchRepository)
        services.AddCqrs(ServiceLifetime.Scoped, typeof(GitHubDbContext).Assembly);

        return services;
    }
}
