using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;

namespace Olbrasoft.GitHub.Issues.Business.Services;

/// <summary>
/// Service for checking database status using Entity Framework Core.
/// </summary>
public class DatabaseStatusService : IDatabaseStatusService
{
    private readonly GitHubDbContext _context;
    private readonly ILogger<DatabaseStatusService> _logger;

    public DatabaseStatusService(GitHubDbContext context, ILogger<DatabaseStatusService> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        _context = context;
        _logger = logger;
    }

    public async Task<DatabaseStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Check connection
            var canConnect = await _context.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
            {
                return new DatabaseStatus
                {
                    CanConnect = false,
                    StatusCode = DatabaseStatusCode.ConnectionError,
                    StatusMessage = "Nelze se připojit k databázi.",
                    ErrorMessage = "Database connection failed"
                };
            }

            // Check pending migrations
            var pendingMigrations = (await _context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            var hasPendingMigrations = pendingMigrations.Count > 0;

            // Check if tables exist
            var appliedMigrations = (await _context.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
            var hasTables = appliedMigrations.Count > 0;

            // If no tables at all (no migrations ever applied)
            if (!hasTables && hasPendingMigrations)
            {
                return new DatabaseStatus
                {
                    CanConnect = true,
                    HasTables = false,
                    PendingMigrationCount = pendingMigrations.Count,
                    PendingMigrations = pendingMigrations,
                    StatusCode = DatabaseStatusCode.EmptyDatabase,
                    StatusMessage = "Databáze je prázdná. Pro správné fungování je potřeba vytvořit tabulky."
                };
            }

            // If tables exist but pending migrations
            if (hasPendingMigrations)
            {
                return new DatabaseStatus
                {
                    CanConnect = true,
                    HasTables = true,
                    PendingMigrationCount = pendingMigrations.Count,
                    PendingMigrations = pendingMigrations,
                    StatusCode = DatabaseStatusCode.PendingMigrations,
                    StatusMessage = "Struktura databáze byla změněna. Pro správné fungování je potřeba provést aktualizaci."
                };
            }

            // Check data counts
            var issueCount = await _context.Issues.CountAsync(cancellationToken);
            var repositoryCount = await _context.Repositories.CountAsync(cancellationToken);

            if (issueCount == 0)
            {
                return new DatabaseStatus
                {
                    CanConnect = true,
                    HasTables = true,
                    PendingMigrationCount = 0,
                    PendingMigrations = Array.Empty<string>(),
                    IssueCount = 0,
                    RepositoryCount = repositoryCount,
                    StatusCode = DatabaseStatusCode.NoData,
                    StatusMessage = "Databáze je připravena, ale neobsahuje žádná data. Klikněte na 'Importovat data' pro synchronizaci z GitHubu."
                };
            }

            // Everything OK
            return new DatabaseStatus
            {
                CanConnect = true,
                HasTables = true,
                PendingMigrationCount = 0,
                PendingMigrations = Array.Empty<string>(),
                IssueCount = issueCount,
                RepositoryCount = repositoryCount,
                StatusCode = DatabaseStatusCode.Healthy,
                StatusMessage = $"Databáze obsahuje {issueCount} issues z {repositoryCount} repozitářů."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking database status");
            return new DatabaseStatus
            {
                CanConnect = false,
                StatusCode = DatabaseStatusCode.ConnectionError,
                StatusMessage = "Chyba při kontrole databáze.",
                ErrorMessage = ex.Message
            };
        }
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
