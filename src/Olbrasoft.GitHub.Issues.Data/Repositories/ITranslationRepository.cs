using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Data.Repositories;

/// <summary>
/// Repository abstraction for translation-related database operations.
/// Manages Issue lookups and CachedText storage for title translations.
/// Follows Dependency Inversion Principle.
/// </summary>
public interface ITranslationRepository
{
    /// <summary>
    /// Gets an issue by its ID for translation purposes.
    /// </summary>
    /// <param name="issueId">Internal issue ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Issue entity or null if not found</returns>
    Task<Issue?> GetIssueByIdAsync(int issueId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a cached translation for an issue.
    /// </summary>
    /// <param name="issueId">Internal issue ID</param>
    /// <param name="languageId">Language LCID (e.g., 1029 for Czech)</param>
    /// <param name="textTypeId">Text type ID (e.g., Title = 1)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached translation or null if not found</returns>
    Task<CachedText?> GetCachedTranslationAsync(
        int issueId,
        int languageId,
        int textTypeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a cached translation from the database.
    /// Used when cache is stale (issue was updated after translation was cached).
    /// </summary>
    /// <param name="cachedText">Cached text entity to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteCachedTextAsync(CachedText cachedText, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a new cached translation to the database.
    /// </summary>
    /// <param name="cachedText">Cached text entity to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SaveCachedTextAsync(CachedText cachedText, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets cached text if it exists AND is fresh (not older than issue update).
    /// Automatically deletes stale cache entries.
    /// </summary>
    /// <param name="issueId">Internal issue ID</param>
    /// <param name="languageId">Language LCID</param>
    /// <param name="textTypeId">Text type ID</param>
    /// <param name="issueUpdatedAt">Issue last updated timestamp for freshness validation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached text content if found and fresh; otherwise null</returns>
    Task<string?> GetIfFreshAsync(
        int issueId,
        int languageId,
        int textTypeId,
        DateTime issueUpdatedAt,
        CancellationToken cancellationToken = default);
}
