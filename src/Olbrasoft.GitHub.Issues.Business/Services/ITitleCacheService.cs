namespace Olbrasoft.GitHub.Issues.Business.Services;

/// <summary>
/// Service for managing title translation cache operations.
/// Handles cache retrieval, freshness validation, and storage for title translations.
/// </summary>
public interface ITitleCacheService
{
    /// <summary>
    /// Gets cached title translation if it exists and is fresh.
    /// Validates cache against issue update timestamp - if issue was updated after cache, returns null.
    /// </summary>
    /// <param name="issueId">The database issue ID</param>
    /// <param name="languageId">The language ID (LCID)</param>
    /// <param name="issueUpdatedAt">The GitHub issue last updated timestamp</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached title if fresh, null if missing or stale</returns>
    Task<string?> GetCachedTitleAsync(
        int issueId,
        int languageId,
        DateTime issueUpdatedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves title translation to cache.
    /// Handles duplicate key exceptions gracefully (concurrent inserts).
    /// </summary>
    /// <param name="issueId">The database issue ID</param>
    /// <param name="languageId">The language ID (LCID)</param>
    /// <param name="content">The translated title</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SaveTitleAsync(
        int issueId,
        int languageId,
        string content,
        CancellationToken cancellationToken = default);
}
