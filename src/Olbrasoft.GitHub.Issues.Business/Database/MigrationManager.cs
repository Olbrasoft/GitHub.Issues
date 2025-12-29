using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;

namespace Olbrasoft.GitHub.Issues.Business.Database;

/// <summary>
/// Service for managing database migrations (write operations).
/// Implements Single Responsibility Principle and Command-Query Separation by separating write operations from read operations.
/// </summary>
public class MigrationManager : IMigrationManager
{
    private readonly GitHubDbContext _context;
    private readonly ILogger<MigrationManager> _logger;

    public MigrationManager(
        GitHubDbContext context,
        ILogger<MigrationManager> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        _context = context;
        _logger = logger;
    }

    public async Task<MigrationResult> ApplyMigrationsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var pendingMigrations = (await _context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

            if (pendingMigrations.Count == 0)
            {
                return new MigrationResult
                {
                    Success = true,
                    MigrationsApplied = 0,
                    AppliedMigrations = Array.Empty<string>()
                };
            }

            _logger.LogInformation("Applying {Count} pending migrations", pendingMigrations.Count);

            await _context.Database.MigrateAsync(cancellationToken);

            _logger.LogInformation("Successfully applied migrations: {Migrations}", string.Join(", ", pendingMigrations));

            return new MigrationResult
            {
                Success = true,
                MigrationsApplied = pendingMigrations.Count,
                AppliedMigrations = pendingMigrations
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying migrations");
            return new MigrationResult
            {
                Success = false,
                MigrationsApplied = 0,
                AppliedMigrations = Array.Empty<string>(),
                ErrorMessage = ex.Message
            };
        }
    }
}
