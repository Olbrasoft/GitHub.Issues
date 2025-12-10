using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Repositories;

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

        // Register DbContext with provider-specific options
        services.AddDbContext<GitHubDbContext>((serviceProvider, options) =>
        {
            switch (provider)
            {
                case DatabaseProvider.PostgreSQL:
                    options.UseNpgsql(connectionString, npgsqlOptions =>
                    {
                        npgsqlOptions.UseVector();
                    });
                    break;

                case DatabaseProvider.SqlServer:
                    options.UseSqlServer(connectionString);
                    break;

                default:
                    throw new ArgumentException($"Unsupported database provider: {provider}");
            }
        });

        // Register provider-specific vector search repository
        switch (provider)
        {
            case DatabaseProvider.PostgreSQL:
                services.AddScoped<IVectorSearchRepository, PostgreSqlVectorSearchRepository>();
                break;

            case DatabaseProvider.SqlServer:
                services.AddScoped<IVectorSearchRepository, SqlServerVectorSearchRepository>();
                break;
        }

        return services;
    }
}
