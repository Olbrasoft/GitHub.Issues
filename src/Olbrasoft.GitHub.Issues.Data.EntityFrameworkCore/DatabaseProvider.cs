namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;

/// <summary>
/// Supported database providers for multi-provider EF Core configuration.
/// </summary>
public enum DatabaseProvider
{
    /// <summary>
    /// PostgreSQL with pgvector extension for vector operations.
    /// Used in development environment.
    /// </summary>
    PostgreSQL,

    /// <summary>
    /// SQL Server with native VECTOR type support (Azure SQL).
    /// Used in production environment.
    /// </summary>
    SqlServer
}

/// <summary>
/// Database configuration settings.
/// </summary>
public class DatabaseSettings
{
    /// <summary>
    /// The database provider to use.
    /// </summary>
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.PostgreSQL;
}
