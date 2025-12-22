using Olbrasoft.GitHub.Issues.Data.Dtos;
using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Sync.Services;

/// <summary>
/// Service for synchronizing issues from GitHub.
/// </summary>
public interface IIssueSyncService
{
    /// <summary>
    /// Analyzes what will be synced without performing any actual sync or API calls to embedding providers.
    /// </summary>
    /// <returns>Analysis showing what would be synced.</returns>
    Task<SyncAnalysisDto> AnalyzeChangesAsync(
        Repository repository,
        string owner,
        string repo,
        DateTimeOffset? since = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronizes issues from GitHub for a repository.
    /// </summary>
    /// <param name="generateEmbeddings">If true, generates embeddings via Cohere API. If false, saves issues without embeddings (Embedding = null).</param>
    /// <returns>Statistics about the sync operation.</returns>
    Task<SyncStatisticsDto> SyncIssuesAsync(
        Repository repository,
        string owner,
        string repo,
        DateTimeOffset? since = null,
        bool generateEmbeddings = true,
        CancellationToken cancellationToken = default);
}
