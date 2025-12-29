namespace Olbrasoft.GitHub.Issues.Business;

/// <summary>
/// Read-only service for checking database health and status.
/// Separated from migration operations following Single Responsibility Principle.
/// </summary>
public interface IDatabaseHealthChecker
{
    /// <summary>
    /// Gets the current database status including connection, migration, and data information.
    /// This is a read-only operation that does not modify the database.
    /// </summary>
    Task<DatabaseStatus> GetStatusAsync(CancellationToken cancellationToken = default);
}
