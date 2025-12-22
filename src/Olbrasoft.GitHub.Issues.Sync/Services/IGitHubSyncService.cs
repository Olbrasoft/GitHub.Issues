using Olbrasoft.GitHub.Issues.Data.Dtos;

namespace Olbrasoft.GitHub.Issues.Sync.Services;

public interface IGitHubSyncService
{
    /// <summary>
    /// Analyzes what will be synced for a single repository without performing actual sync or API calls to embedding providers.
    /// </summary>
    /// <param name="owner">Repository owner</param>
    /// <param name="repo">Repository name</param>
    /// <param name="since">If provided, only analyze issues changed since this timestamp (incremental). If null, full analysis.</param>
    /// <param name="smartMode">If true, automatically use stored last_synced_at timestamp from DB</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Analysis showing what would be synced.</returns>
    Task<SyncAnalysisDto> AnalyzeRepositoryAsync(string owner, string repo, DateTimeOffset? since = null, bool smartMode = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronizes a single repository.
    /// </summary>
    /// <param name="owner">Repository owner</param>
    /// <param name="repo">Repository name</param>
    /// <param name="since">If provided, only sync issues changed since this timestamp (incremental). If null, full sync.</param>
    /// <param name="smartMode">If true, automatically use stored last_synced_at timestamp from DB</param>
    /// <param name="generateEmbeddings">If true, generates embeddings via Cohere API. If false, saves issues without embeddings.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Statistics about the sync operation.</returns>
    Task<SyncStatisticsDto> SyncRepositoryAsync(string owner, string repo, DateTimeOffset? since = null, bool smartMode = false, bool generateEmbeddings = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronizes all repositories based on configuration:
    /// - If Repositories list is not empty, uses explicit list
    /// - Otherwise, if Owner is set, discovers repos via API
    /// </summary>
    /// <param name="since">If provided, only sync issues changed since this timestamp (incremental). If null, full sync.</param>
    /// <param name="smartMode">If true, automatically use stored last_synced_at timestamp from DB</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Aggregated statistics about the sync operation.</returns>
    Task<SyncStatisticsDto> SyncAllRepositoriesAsync(DateTimeOffset? since = null, bool smartMode = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronizes a list of repositories provided as arguments.
    /// </summary>
    /// <param name="repositories">List of repositories in "owner/repo" format</param>
    /// <param name="since">If provided, only sync issues changed since this timestamp (incremental). If null, full sync.</param>
    /// <param name="smartMode">If true, automatically use stored last_synced_at timestamp from DB</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Aggregated statistics about the sync operation.</returns>
    Task<SyncStatisticsDto> SyncRepositoriesAsync(IEnumerable<string> repositories, DateTimeOffset? since = null, bool smartMode = false, CancellationToken cancellationToken = default);
}
