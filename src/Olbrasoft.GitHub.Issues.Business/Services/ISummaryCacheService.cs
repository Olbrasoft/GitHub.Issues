namespace Olbrasoft.GitHub.Issues.Business.Services;

/// <summary>
/// Service for managing summary cache operations.
/// Single Responsibility: Cache retrieval, validation, and storage for issue summaries.
/// </summary>
public interface ISummaryCacheService
{
    /// <summary>
    /// Gets cached summary if it exists and is fresh (not older than issue update).
    /// </summary>
    /// <param name="issueId">Database issue ID</param>
    /// <param name="languageId">Language ID (e.g., 1=en-US, 2=cs-CZ)</param>
    /// <param name="textTypeId">Text type ID (e.g., ListSummary)</param>
    /// <param name="issueUpdatedAt">Issue last updated timestamp for freshness validation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached summary content if found and fresh; otherwise null</returns>
    Task<string?> GetCachedSummaryAsync(
        int issueId,
        int languageId,
        int textTypeId,
        DateTime issueUpdatedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves summary text to cache with current timestamp.
    /// </summary>
    /// <param name="issueId">Database issue ID</param>
    /// <param name="languageId">Language ID</param>
    /// <param name="textTypeId">Text type ID</param>
    /// <param name="content">Summary content to cache</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SaveSummaryAsync(
        int issueId,
        int languageId,
        int textTypeId,
        string content,
        CancellationToken cancellationToken = default);
}
