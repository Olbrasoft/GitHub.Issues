using Microsoft.Extensions.Logging;
using Olbrasoft.GitHub.Issues.Business;
using Olbrasoft.GitHub.Issues.Data.Dtos;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Sync.ApiClients;

namespace Olbrasoft.GitHub.Issues.Sync.Services;

/// <summary>
/// Orchestrates issue synchronization from GitHub.
/// Single responsibility: Coordinate sync workflow between API client, embedding, and business services.
/// </summary>
public class IssueSyncService : IIssueSyncService
{
    private readonly IGitHubIssueApiClient _apiClient;
    private readonly IIssueSyncBusinessService _issueSyncBusiness;
    private readonly IIssueEmbeddingGenerator _embeddingGenerator;
    private readonly ILogger<IssueSyncService> _logger;

    public IssueSyncService(
        IGitHubIssueApiClient apiClient,
        IIssueSyncBusinessService issueSyncBusiness,
        IIssueEmbeddingGenerator embeddingGenerator,
        ILogger<IssueSyncService> logger)
    {
        _apiClient = apiClient;
        _issueSyncBusiness = issueSyncBusiness;
        _embeddingGenerator = embeddingGenerator;
        _logger = logger;
    }

    public async Task<SyncAnalysisDto> AnalyzeChangesAsync(
        Repository repository,
        string owner,
        string repo,
        DateTimeOffset? since = null,
        CancellationToken cancellationToken = default)
    {
        // Fetch issues from GitHub API (only GitHub call, NO Cohere calls)
        var allIssues = await _apiClient.FetchIssuesAsync(owner, repo, since, cancellationToken);

        // Get existing issues for change detection
        var existingIssues = await _issueSyncBusiness.GetIssuesByRepositoryAsync(repository.Id, cancellationToken);

        int newCount = 0;
        int changedCount = 0;
        int missingEmbeddings = 0;

        foreach (var ghIssue in allIssues)
        {
            // Skip pull requests
            if (ghIssue.IsPullRequest)
            {
                continue;
            }

            // Check if issue exists and has changed
            var existingIssue = existingIssues.GetValueOrDefault(ghIssue.Number);
            var isNew = existingIssue == null;
            var hasChanged = !isNew && existingIssue!.GitHubUpdatedAt < ghIssue.UpdatedAt;

            if (isNew)
            {
                newCount++;
            }
            else if (hasChanged)
            {
                changedCount++;
            }

            // Check if existing issue is missing embedding
            if (existingIssue != null && existingIssue.Embedding == null)
            {
                missingEmbeddings++;
            }
        }

        return new SyncAnalysisDto
        {
            RepositoryFullName = $"{owner}/{repo}",
            TotalIssues = allIssues.Count(i => !i.IsPullRequest),
            NewIssues = newCount,
            ChangedIssues = changedCount,
            MissingEmbeddings = missingEmbeddings
        };
    }

    public async Task<SyncStatisticsDto> SyncIssuesAsync(
        Repository repository,
        string owner,
        string repo,
        DateTimeOffset? since = null,
        CancellationToken cancellationToken = default)
    {
        var stats = new SyncStatisticsDto
        {
            SinceTimestamp = since
        };

        // Fetch issues from GitHub API
        var allIssues = await _apiClient.FetchIssuesAsync(owner, repo, since, cancellationToken);
        stats.ApiCalls++; // Count API call

        // Get existing issues for change detection
        var existingIssues = await _issueSyncBusiness.GetIssuesByRepositoryAsync(repository.Id, cancellationToken);

        var syncedAt = DateTimeOffset.UtcNow;
        var parentChildRelationships = new Dictionary<int, string>();

        foreach (var ghIssue in allIssues)
        {
            // Skip pull requests
            if (ghIssue.IsPullRequest)
            {
                continue;
            }

            stats.TotalFound++;

            // Track parent-child relationships for later
            if (!string.IsNullOrEmpty(ghIssue.ParentIssueUrl))
            {
                parentChildRelationships[ghIssue.Number] = ghIssue.ParentIssueUrl;
            }

            // Check if issue exists and has changed
            var existingIssue = existingIssues.GetValueOrDefault(ghIssue.Number);
            var isNew = existingIssue == null;
            var hasChanged = !isNew && existingIssue!.GitHubUpdatedAt < ghIssue.UpdatedAt;

            // Generate embedding for new/changed issues (includes comments)
            float[]? embedding;
            if (isNew || hasChanged)
            {
                embedding = await _embeddingGenerator.GenerateEmbeddingAsync(
                    owner, repo, ghIssue.Number, ghIssue.Title, ghIssue.Body, ghIssue.LabelNames, cancellationToken);
                stats.ApiCalls++; // Count comment API call
            }
            else
            {
                embedding = existingIssue!.Embedding;
            }
            if (embedding == null)
            {
                _logger.LogWarning(
                    "Issue #{Number} ({Title}): Could not generate embedding - SKIPPING issue (embedding is required).",
                    ghIssue.Number, ghIssue.Title);
                stats.EmbeddingsFailed++;
                continue; // Skip issue - embedding is required
            }

            // Save issue
            var savedIssue = await _issueSyncBusiness.SaveIssueAsync(
                repository.Id,
                ghIssue.Number,
                ghIssue.Title,
                ghIssue.State == "open",
                ghIssue.HtmlUrl,
                ghIssue.UpdatedAt,
                syncedAt,
                embedding,
                cancellationToken);

            // Track statistics
            if (isNew)
            {
                stats.Created++;
            }
            else if (hasChanged)
            {
                stats.Updated++;
            }
            else
            {
                stats.Unchanged++;
            }

            // Sync labels
            if (ghIssue.LabelNames.Count > 0)
            {
                await _issueSyncBusiness.SyncLabelsAsync(
                    savedIssue.Id,
                    repository.Id,
                    ghIssue.LabelNames.ToList(),
                    cancellationToken);
            }

            _logger.LogDebug("Synced issue #{Number}: {Title}", ghIssue.Number, ghIssue.Title);
        }

        // Update parent-child relationships
        await UpdateParentChildRelationshipsAsync(repository.Id, parentChildRelationships, cancellationToken);

        // Detect and mark deleted issues during FULL SYNC only
        if (since == null)
        {
            stats.Deleted = await DetectAndMarkDeletedIssuesAsync(
                repository.Id,
                existingIssues,
                allIssues.Select(i => i.Number).ToHashSet(),
                cancellationToken);
        }

        return stats;
    }

    /// <summary>
    /// Detects issues that exist in DB but were not returned from GitHub during full sync.
    /// These are marked as deleted (soft delete).
    /// </summary>
    private async Task<int> DetectAndMarkDeletedIssuesAsync(
        int repositoryId,
        Dictionary<int, Issue> existingIssues,
        HashSet<int> githubIssueNumbers,
        CancellationToken cancellationToken)
    {
        // Find issues in DB that were not returned from GitHub (excluding already deleted)
        var issueIdsToDelete = existingIssues
            .Where(kvp => !githubIssueNumbers.Contains(kvp.Key) && !kvp.Value.IsDeleted)
            .Select(kvp => kvp.Value.Id)
            .ToList();

        if (issueIdsToDelete.Count == 0)
        {
            return 0;
        }

        _logger.LogInformation(
            "Marking {Count} issues as deleted (not found on GitHub during full sync)",
            issueIdsToDelete.Count);

        // Log which issues are being deleted
        foreach (var kvp in existingIssues.Where(kvp => !githubIssueNumbers.Contains(kvp.Key) && !kvp.Value.IsDeleted))
        {
            _logger.LogInformation("  Issue #{Number}: {Title}", kvp.Key, kvp.Value.Title);
        }

        var deletedCount = await _issueSyncBusiness.MarkIssuesAsDeletedAsync(
            repositoryId,
            issueIdsToDelete,
            cancellationToken);

        return deletedCount;
    }

    private async Task UpdateParentChildRelationshipsAsync(
        int repositoryId,
        Dictionary<int, string> parentChildRelationships,
        CancellationToken cancellationToken)
    {
        if (parentChildRelationships.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Updating {Count} parent-child relationships", parentChildRelationships.Count);

        var issuesByNumber = await _issueSyncBusiness.GetIssuesByRepositoryAsync(repositoryId, cancellationToken);
        var childToParentMap = new Dictionary<int, int?>();

        foreach (var (childNumber, parentUrl) in parentChildRelationships)
        {
            var parentNumber = ExtractIssueNumberFromUrl(parentUrl);
            if (parentNumber.HasValue &&
                issuesByNumber.TryGetValue(parentNumber.Value, out var parentIssue) &&
                issuesByNumber.TryGetValue(childNumber, out var childIssue))
            {
                if (childIssue.ParentIssueId != parentIssue.Id)
                {
                    childToParentMap[childIssue.Id] = parentIssue.Id;
                    _logger.LogDebug("Set parent of issue #{Child} to #{Parent}", childNumber, parentNumber.Value);
                }
            }
        }

        if (childToParentMap.Count > 0)
        {
            var updatedCount = await _issueSyncBusiness.BatchSetParentsAsync(childToParentMap, cancellationToken);
            _logger.LogInformation("Updated {Count} parent-child relationships", updatedCount);
        }
    }

    private static int? ExtractIssueNumberFromUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        var lastSlashIndex = url.LastIndexOf('/');
        if (lastSlashIndex >= 0 && lastSlashIndex < url.Length - 1)
        {
            var numberPart = url[(lastSlashIndex + 1)..];
            if (int.TryParse(numberPart, out var number))
            {
                return number;
            }
        }

        return null;
    }
}
