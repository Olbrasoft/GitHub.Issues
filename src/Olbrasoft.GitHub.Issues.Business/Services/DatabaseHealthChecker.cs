using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.Repositories;

namespace Olbrasoft.GitHub.Issues.Business.Services;

/// <summary>
/// Read-only service for checking database health and status.
/// Implements Single Responsibility Principle by separating read operations from write operations.
/// </summary>
public class DatabaseHealthChecker : IDatabaseHealthChecker
{
    private readonly GitHubDbContext _context;
    private readonly IIssueRepository _issueRepository;
    private readonly IRepositoryRepository _repositoryRepository;
    private readonly ILogger<DatabaseHealthChecker> _logger;

    public DatabaseHealthChecker(
        GitHubDbContext context,
        IIssueRepository issueRepository,
        IRepositoryRepository repositoryRepository,
        ILogger<DatabaseHealthChecker> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(issueRepository);
        ArgumentNullException.ThrowIfNull(repositoryRepository);
        ArgumentNullException.ThrowIfNull(logger);

        _context = context;
        _issueRepository = issueRepository;
        _repositoryRepository = repositoryRepository;
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
            var issueCount = await _issueRepository.CountAsync(cancellationToken);
            var repositoryCount = await _repositoryRepository.CountAsync(cancellationToken);

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
}
