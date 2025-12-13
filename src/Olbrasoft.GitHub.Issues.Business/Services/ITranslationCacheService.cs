namespace Olbrasoft.GitHub.Issues.Business.Services;

/// <summary>
/// Service for managing translation cache invalidation.
/// </summary>
public interface ITranslationCacheService
{
    /// <summary>
    /// Invalidates all cached translations for an issue.
    /// Call this when issue title or body changes.
    /// </summary>
    /// <param name="issueId">The issue ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Number of deleted cache entries</returns>
    Task<int> InvalidateAsync(int issueId, CancellationToken ct = default);

    /// <summary>
    /// Invalidates specific text type translations for an issue.
    /// Call with TextTypeCode.Title when only title changes.
    /// Call with TextTypeCode.ListSummary/DetailSummary when body changes.
    /// </summary>
    /// <param name="issueId">The issue ID</param>
    /// <param name="textTypeId">The text type ID to invalidate</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Number of deleted cache entries</returns>
    Task<int> InvalidateByTextTypeAsync(int issueId, int textTypeId, CancellationToken ct = default);

    /// <summary>
    /// Invalidates all cached translations for issues in specified repositories.
    /// </summary>
    /// <param name="repositoryIds">Repository IDs to invalidate</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Number of deleted cache entries</returns>
    Task<int> InvalidateByRepositoriesAsync(IEnumerable<int> repositoryIds, CancellationToken ct = default);

    /// <summary>
    /// Invalidates entire translation cache.
    /// Use with caution - admin operation only.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Number of deleted cache entries</returns>
    Task<int> InvalidateAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Cache statistics</returns>
    Task<CacheStatistics> GetStatisticsAsync(CancellationToken ct = default);
}

/// <summary>
/// Cache statistics for admin UI.
/// </summary>
public class CacheStatistics
{
    public int TotalRecords { get; set; }
    public Dictionary<string, int> ByLanguage { get; set; } = new();
    public Dictionary<string, int> ByTextType { get; set; } = new();
}
