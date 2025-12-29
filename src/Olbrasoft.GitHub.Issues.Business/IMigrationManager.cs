namespace Olbrasoft.GitHub.Issues.Business;

/// <summary>
/// Service for managing database migrations (write operations).
/// Separated from health checking following Single Responsibility Principle and Command-Query Separation.
/// </summary>
public interface IMigrationManager
{
    /// <summary>
    /// Applies any pending database migrations.
    /// This is a dangerous write operation that modifies the database schema.
    /// </summary>
    /// <returns>Result indicating success and which migrations were applied.</returns>
    Task<MigrationResult> ApplyMigrationsAsync(CancellationToken cancellationToken = default);
}
