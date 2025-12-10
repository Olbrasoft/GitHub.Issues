using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;

namespace Olbrasoft.GitHub.Issues.Sync.Services;

/// <summary>
/// Orchestrator service for synchronizing GitHub data.
/// Coordinates the work of specialized sync services.
/// </summary>
public class GitHubSyncService : IGitHubSyncService
{
    private readonly GitHubDbContext _dbContext;
    private readonly IRepositorySyncService _repositorySyncService;
    private readonly ILabelSyncService _labelSyncService;
    private readonly IIssueSyncService _issueSyncService;
    private readonly IEventSyncService _eventSyncService;
    private readonly GitHubSettings _settings;
    private readonly ILogger<GitHubSyncService> _logger;

    public GitHubSyncService(
        GitHubDbContext dbContext,
        IRepositorySyncService repositorySyncService,
        ILabelSyncService labelSyncService,
        IIssueSyncService issueSyncService,
        IEventSyncService eventSyncService,
        IOptions<GitHubSettings> settings,
        ILogger<GitHubSyncService> logger)
    {
        _dbContext = dbContext;
        _repositorySyncService = repositorySyncService;
        _labelSyncService = labelSyncService;
        _issueSyncService = issueSyncService;
        _eventSyncService = eventSyncService;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SyncAllRepositoriesAsync(DateTimeOffset? since = null, bool smartMode = false, CancellationToken cancellationToken = default)
    {
        IEnumerable<string> repositories;

        if (_settings.Repositories.Count > 0)
        {
            // Use explicit list from config
            _logger.LogInformation("Using explicit repository list from configuration ({Count} repositories)", _settings.Repositories.Count);
            repositories = _settings.Repositories;
        }
        else if (!string.IsNullOrEmpty(_settings.Owner))
        {
            // Discover repos via API
            _logger.LogInformation("Discovering repositories for owner: {Owner} (type: {OwnerType})", _settings.Owner, _settings.OwnerType);
            repositories = await _repositorySyncService.FetchAllRepositoriesForOwnerAsync(cancellationToken);
        }
        else
        {
            _logger.LogWarning("No repositories to sync. Configure either 'Repositories' list or 'Owner' in settings.");
            return;
        }

        await SyncRepositoriesAsync(repositories, since, smartMode, cancellationToken);
    }

    public async Task SyncRepositoriesAsync(IEnumerable<string> repositories, DateTimeOffset? since = null, bool smartMode = false, CancellationToken cancellationToken = default)
    {
        foreach (var repoFullName in repositories)
        {
            var parts = repoFullName.Split('/');
            if (parts.Length != 2)
            {
                _logger.LogWarning("Invalid repository format: {Repository}. Expected 'owner/repo'", repoFullName);
                continue;
            }

            await SyncRepositoryAsync(parts[0], parts[1], since, smartMode, cancellationToken);
        }
    }

    public async Task SyncRepositoryAsync(string owner, string repo, DateTimeOffset? since = null, bool smartMode = false, CancellationToken cancellationToken = default)
    {
        var repository = await _repositorySyncService.EnsureRepositoryAsync(owner, repo, cancellationToken);

        // In smart mode, use stored last_synced_at if available
        var effectiveSince = since;
        if (smartMode && !since.HasValue && repository.LastSyncedAt.HasValue)
        {
            effectiveSince = repository.LastSyncedAt;
            _logger.LogInformation("Smart sync: using stored timestamp {Timestamp:u} for {Owner}/{Repo}",
                effectiveSince.Value, owner, repo);
        }

        var mode = smartMode ? "smart" : (effectiveSince.HasValue ? "incremental" : "full");
        var isFirstSync = smartMode && !repository.LastSyncedAt.HasValue;

        if (isFirstSync)
        {
            _logger.LogInformation("Starting {Mode} sync for {Owner}/{Repo} (first sync - full download)",
                mode, owner, repo);
        }
        else
        {
            _logger.LogInformation("Starting {Mode} sync for {Owner}/{Repo}{Since}",
                mode, owner, repo,
                effectiveSince.HasValue ? $" (since {effectiveSince.Value:u})" : "");
        }

        // Orchestrate sync operations
        await _labelSyncService.SyncLabelsAsync(repository, owner, repo, cancellationToken);
        await _issueSyncService.SyncIssuesAsync(repository, owner, repo, effectiveSince, cancellationToken);
        await _eventSyncService.SyncEventsAsync(repository, owner, repo, effectiveSince, cancellationToken);

        // Update last synced timestamp
        repository.LastSyncedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Completed sync for {Owner}/{Repo}", owner, repo);
    }
}
