using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.GitHub.Issues.Business;
using Olbrasoft.GitHub.Issues.Data.Dtos;

namespace Olbrasoft.GitHub.Issues.Sync.Services;

/// <summary>
/// Orchestrator service for synchronizing GitHub data.
/// Coordinates the work of specialized sync services.
/// </summary>
public class GitHubSyncService : IGitHubSyncService
{
    private readonly IRepositorySyncBusinessService _repositorySyncBusiness;
    private readonly IRepositorySyncService _repositorySyncService;
    private readonly ILabelSyncService _labelSyncService;
    private readonly IIssueSyncService _issueSyncService;
    private readonly IEventSyncService _eventSyncService;
    private readonly GitHubSettings _settings;
    private readonly ILogger<GitHubSyncService> _logger;

    public GitHubSyncService(
        IRepositorySyncBusinessService repositorySyncBusiness,
        IRepositorySyncService repositorySyncService,
        ILabelSyncService labelSyncService,
        IIssueSyncService issueSyncService,
        IEventSyncService eventSyncService,
        IOptions<GitHubSettings> settings,
        ILogger<GitHubSyncService> logger)
    {
        _repositorySyncBusiness = repositorySyncBusiness;
        _repositorySyncService = repositorySyncService;
        _labelSyncService = labelSyncService;
        _issueSyncService = issueSyncService;
        _eventSyncService = eventSyncService;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<SyncStatisticsDto> SyncAllRepositoriesAsync(DateTimeOffset? since = null, bool smartMode = false, bool generateEmbeddings = true, CancellationToken cancellationToken = default)
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
            return new SyncStatisticsDto();
        }

        return await SyncRepositoriesAsync(repositories, since, smartMode, generateEmbeddings, cancellationToken);
    }

    public async Task<SyncStatisticsDto> SyncRepositoriesAsync(IEnumerable<string> repositories, DateTimeOffset? since = null, bool smartMode = false, bool generateEmbeddings = true, CancellationToken cancellationToken = default)
    {
        var aggregatedStats = new SyncStatisticsDto();

        foreach (var repoFullName in repositories)
        {
            var parts = repoFullName.Split('/');
            if (parts.Length != 2)
            {
                _logger.LogWarning("Invalid repository format: {Repository}. Expected 'owner/repo'", repoFullName);
                continue;
            }

            var repoStats = await SyncRepositoryAsync(parts[0], parts[1], since, smartMode, generateEmbeddings, cancellationToken);
            aggregatedStats.Add(repoStats);

            // Keep the first since timestamp (for display purposes)
            aggregatedStats.SinceTimestamp ??= repoStats.SinceTimestamp;
        }

        return aggregatedStats;
    }

    public async Task<SyncAnalysisDto> AnalyzeRepositoryAsync(string owner, string repo, DateTimeOffset? since = null, bool smartMode = false, CancellationToken cancellationToken = default)
    {
        var repository = await _repositorySyncService.EnsureRepositoryAsync(owner, repo, cancellationToken);

        // In smart mode, use stored last_synced_at if available
        var effectiveSince = since;
        if (smartMode && !since.HasValue && repository.LastSyncedAt.HasValue)
        {
            effectiveSince = repository.LastSyncedAt;
            _logger.LogInformation("Smart analysis: using stored timestamp {Timestamp:u} for {Owner}/{Repo}",
                effectiveSince.Value, owner, repo);
        }

        _logger.LogInformation("Analyzing changes for {Owner}/{Repo}{Since}",
            owner, repo,
            effectiveSince.HasValue ? $" (since {effectiveSince.Value:u})" : "");

        // Analyze issues (NO Cohere API calls, only GitHub)
        var analysis = await _issueSyncService.AnalyzeChangesAsync(repository, owner, repo, effectiveSince, cancellationToken);

        _logger.LogInformation("Analysis completed for {Owner}/{Repo}: Total={Total}, New={New}, Changed={Changed}, RequiredCohereApiCalls={ApiCalls}",
            owner, repo, analysis.TotalIssues, analysis.NewIssues, analysis.ChangedIssues, analysis.RequiredCohereApiCalls);

        return analysis;
    }

    public async Task<SyncStatisticsDto> SyncRepositoryAsync(string owner, string repo, DateTimeOffset? since = null, bool smartMode = false, bool generateEmbeddings = true, CancellationToken cancellationToken = default)
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
        var embeddingMode = generateEmbeddings ? "with embeddings" : "without embeddings";

        if (isFirstSync)
        {
            _logger.LogInformation("Starting {Mode} sync for {Owner}/{Repo} (first sync - full download, {EmbeddingMode})",
                mode, owner, repo, embeddingMode);
        }
        else
        {
            _logger.LogInformation("Starting {Mode} sync for {Owner}/{Repo}{Since} ({EmbeddingMode})",
                mode, owner, repo,
                effectiveSince.HasValue ? $" (since {effectiveSince.Value:u})" : "",
                embeddingMode);
        }

        // Orchestrate sync operations
        await _labelSyncService.SyncLabelsAsync(repository, owner, repo, cancellationToken);
        var stats = await _issueSyncService.SyncIssuesAsync(repository, owner, repo, effectiveSince, generateEmbeddings, cancellationToken);
        await _eventSyncService.SyncEventsAsync(repository, owner, repo, effectiveSince, cancellationToken);

        // Update last synced timestamp ONLY if sync was successful
        // Don't update if all issues failed to sync (embedding failures)
        var successfullyProcessed = stats.Created + stats.Updated + stats.Unchanged;
        if (successfullyProcessed > 0 || stats.TotalFound == 0)
        {
            await _repositorySyncBusiness.UpdateLastSyncedAsync(repository.Id, DateTimeOffset.UtcNow, cancellationToken);
        }
        else if (stats.EmbeddingsFailed > 0)
        {
            _logger.LogWarning("Not updating LastSyncedAt because all {Count} issues failed embedding generation", stats.EmbeddingsFailed);
        }

        _logger.LogInformation("Completed sync for {Owner}/{Repo}: Found={Found}, Created={Created}, Updated={Updated}, Unchanged={Unchanged}",
            owner, repo, stats.TotalFound, stats.Created, stats.Updated, stats.Unchanged);

        return stats;
    }
}
