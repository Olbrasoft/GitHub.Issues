namespace Olbrasoft.GitHub.Issues.Business.Summarization;

/// <summary>
/// Service for caching AI-generated summaries.
/// Single responsibility: Summary cache operations only (SRP).
/// </summary>
public interface ISummaryCacheService
{
    /// <summary>
    /// Gets cached summary if it exists and is fresh (not stale).
    /// Returns null if cache doesn't exist or is stale.
    /// </summary>
    /// <param name="issueId">Database issue ID</param>
    /// <param name="languageCode">Language code (EnUS or CsCZ)</param>
    /// <param name="issueUpdatedAt">Issue's last update timestamp for freshness validation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached summary or null</returns>
    Task<string?> GetIfFreshAsync(
        int issueId,
        int languageCode,
        DateTime issueUpdatedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves summary to cache.
    /// </summary>
    /// <param name="issueId">Database issue ID</param>
    /// <param name="languageCode">Language code (EnUS or CsCZ)</param>
    /// <param name="summary">Summary text to cache</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SaveAsync(
        int issueId,
        int languageCode,
        string summary,
        CancellationToken cancellationToken = default);
}
