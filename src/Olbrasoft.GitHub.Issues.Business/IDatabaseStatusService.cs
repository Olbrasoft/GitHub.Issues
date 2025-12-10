namespace Olbrasoft.GitHub.Issues.Business;

/// <summary>
/// Service for checking database status, migrations, and data statistics.
/// </summary>
public interface IDatabaseStatusService
{
    /// <summary>
    /// Gets the current database status including connection, migration, and data information.
    /// </summary>
    Task<DatabaseStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies any pending database migrations.
    /// </summary>
    /// <returns>True if migrations were applied successfully, false if no migrations were pending.</returns>
    Task<MigrationResult> ApplyMigrationsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the current database status.
/// </summary>
public record DatabaseStatus
{
    /// <summary>
    /// Whether the database connection is working.
    /// </summary>
    public bool CanConnect { get; init; }

    /// <summary>
    /// Whether the database has all required tables.
    /// </summary>
    public bool HasTables { get; init; }

    /// <summary>
    /// Number of pending migrations.
    /// </summary>
    public int PendingMigrationCount { get; init; }

    /// <summary>
    /// Names of pending migrations.
    /// </summary>
    public IReadOnlyList<string> PendingMigrations { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Total number of synced issues in the database.
    /// </summary>
    public int IssueCount { get; init; }

    /// <summary>
    /// Total number of synced repositories in the database.
    /// </summary>
    public int RepositoryCount { get; init; }

    /// <summary>
    /// Human-readable status message.
    /// </summary>
    public string StatusMessage { get; init; } = string.Empty;

    /// <summary>
    /// Status code for UI rendering.
    /// </summary>
    public DatabaseStatusCode StatusCode { get; init; }

    /// <summary>
    /// Error message if any.
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Status codes for database state.
/// </summary>
public enum DatabaseStatusCode
{
    /// <summary>
    /// Database is healthy and has data.
    /// </summary>
    Healthy,

    /// <summary>
    /// Database exists but has no tables - needs migration.
    /// </summary>
    EmptyDatabase,

    /// <summary>
    /// Database has pending migrations.
    /// </summary>
    PendingMigrations,

    /// <summary>
    /// Database has tables but no data - needs import.
    /// </summary>
    NoData,

    /// <summary>
    /// Cannot connect to database.
    /// </summary>
    ConnectionError
}

/// <summary>
/// Result of a migration operation.
/// </summary>
public record MigrationResult
{
    public bool Success { get; init; }
    public int MigrationsApplied { get; init; }
    public IReadOnlyList<string> AppliedMigrations { get; init; } = Array.Empty<string>();
    public string? ErrorMessage { get; init; }
}
