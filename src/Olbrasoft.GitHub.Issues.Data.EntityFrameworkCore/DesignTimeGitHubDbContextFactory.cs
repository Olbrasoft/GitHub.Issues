using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;

/// <summary>
/// Design-time factory for creating GitHubDbContext with multi-provider support.
/// Used by EF Core CLI tools (dotnet ef migrations) to create migrations.
/// </summary>
/// <remarks>
/// Usage:
/// <code>
/// # PostgreSQL migrations
/// dotnet ef migrations add MigrationName \
///   --startup-project ./src/Olbrasoft.GitHub.Issues.AspNetCore.RazorPages \
///   --project ./src/Olbrasoft.GitHub.Issues.Migrations.PostgreSQL \
///   -- --provider PostgreSQL
///
/// # SQL Server migrations
/// dotnet ef migrations add MigrationName \
///   --startup-project ./src/Olbrasoft.GitHub.Issues.AspNetCore.RazorPages \
///   --project ./src/Olbrasoft.GitHub.Issues.Migrations.SqlServer \
///   -- --provider SqlServer
/// </code>
/// </remarks>
public class DesignTimeGitHubDbContextFactory : IDesignTimeDbContextFactory<GitHubDbContext>
{
    public GitHubDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        var provider = GetProvider(args);
        var connectionString = GetConnectionString(configuration, provider);

        var optionsBuilder = new DbContextOptionsBuilder<GitHubDbContext>();

        switch (provider)
        {
            case DatabaseProvider.PostgreSQL:
                optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.UseVector();
                    npgsqlOptions.MigrationsAssembly("Olbrasoft.GitHub.Issues.Migrations.PostgreSQL");
                })
                .UseSnakeCaseNamingConvention(); // PostgreSQL convention: snake_case
                break;

            case DatabaseProvider.SqlServer:
                optionsBuilder.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.UseVectorSearch(); // Native VECTOR type support
                    sqlOptions.MigrationsAssembly("Olbrasoft.GitHub.Issues.Migrations.SqlServer");
                });
                // SQL Server: PascalCase (EF Core default, no convention needed)
                break;

            default:
                throw new InvalidOperationException($"Unsupported provider: {provider}");
        }

        var settings = new DatabaseSettings { Provider = provider };
        return new GitHubDbContext(optionsBuilder.Options, settings);
    }

    private static DatabaseProvider GetProvider(string[] args)
    {
        // Parse --provider argument from CLI args
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--provider" && i + 1 < args.Length)
            {
                if (Enum.TryParse<DatabaseProvider>(args[i + 1], ignoreCase: true, out var provider))
                {
                    return provider;
                }
                throw new ArgumentException($"Invalid provider value: {args[i + 1]}. Use PostgreSQL or SqlServer.");
            }
        }

        // Default to PostgreSQL for development
        return DatabaseProvider.PostgreSQL;
    }

    private static string GetConnectionString(IConfiguration configuration, DatabaseProvider provider)
    {
        var connectionStringKey = provider switch
        {
            DatabaseProvider.PostgreSQL => "PostgreSQLConnection",
            DatabaseProvider.SqlServer => "SqlServerConnection",
            _ => "DefaultConnection"
        };

        var connectionString = configuration.GetConnectionString(connectionStringKey)
            ?? configuration.GetConnectionString("DefaultConnection");

        // Provide default development connection strings for migrations
        if (string.IsNullOrEmpty(connectionString))
        {
            return provider switch
            {
                DatabaseProvider.PostgreSQL => "Host=localhost;Database=github_issues;Username=postgres;Password=postgres",
                DatabaseProvider.SqlServer => "Server=localhost;Database=GitHubIssues;Trusted_Connection=True;TrustServerCertificate=True",
                _ => throw new InvalidOperationException("No connection string configured")
            };
        }

        return connectionString;
    }
}
