using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Data.Repositories;

/// <summary>
/// Repository abstraction for cached text operations (summaries, translations).
/// Manages cache retrieval, storage, and invalidation for AI-generated content.
/// Follows Dependency Inversion Principle - Business layer depends on this abstraction.
/// </summary>
public interface ICachedTextRepository
{
    /// <summary>
    /// Gets a cached text entry for a specific issue, language, and text type.
    /// </summary>
    /// <param name="issueId">Internal issue ID</param>
    /// <param name="languageId">Language LCID (e.g., 1033 for English, 1029 for Czech)</param>
    /// <param name="textTypeId">Text type ID (1=Title, 2=ListSummary, 3=DetailSummary)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached text entity or null if not found</returns>
    Task<CachedText?> GetByIssueAsync(
        int issueId,
        int languageId,
        int textTypeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets multiple cached text entries for batch operations.
    /// Used by GetForListAsync to load all cached texts in one query.
    /// </summary>
    /// <param name="issueIds">List of issue IDs</param>
    /// <param name="languageId">Language LCID</param>
    /// <param name="textTypeIds">List of text type IDs to retrieve</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of cached text entries</returns>
    Task<List<CachedText>> GetMultipleCachedTextsAsync(
        IEnumerable<int> issueIds,
        int languageId,
        IEnumerable<int> textTypeIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets multiple issues by their IDs for timestamp validation.
    /// </summary>
    /// <param name="issueIds">List of issue IDs</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of issues keyed by ID</returns>
    Task<Dictionary<int, Issue>> GetIssuesByIdsAsync(
        IEnumerable<int> issueIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a new cached text entry to the database.
    /// Handles duplicate key exceptions gracefully (concurrent cache inserts).
    /// </summary>
    /// <param name="cachedText">Cached text entity to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SaveAsync(CachedText cachedText, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves or updates a cached text entry with change tracking support.
    /// Checks if entity is already tracked before adding/updating.
    /// </summary>
    /// <param name="cachedText">Cached text entity to save or update</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SaveOrUpdateAsync(CachedText cachedText, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a cached text entry from the database.
    /// Used when cache is stale (issue was updated after text was cached).
    /// </summary>
    /// <param name="cachedText">Cached text entity to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteAsync(CachedText cachedText, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates (deletes) all cached texts for a specific issue.
    /// </summary>
    /// <param name="issueId">Issue ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of deleted records</returns>
    Task<int> InvalidateByIssueAsync(int issueId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates (deletes) cached texts for a specific issue and text type.
    /// </summary>
    /// <param name="issueId">Issue ID</param>
    /// <param name="textTypeId">Text type ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of deleted records</returns>
    Task<int> InvalidateByIssueAndTextTypeAsync(int issueId, int textTypeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates (deletes) all cached texts for issues in specific repositories.
    /// </summary>
    /// <param name="repositoryIds">Repository IDs</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of deleted records</returns>
    Task<int> InvalidateByRepositoriesAsync(IEnumerable<int> repositoryIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates (deletes) ALL cached texts in the database.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of deleted records</returns>
    Task<int> InvalidateAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets cache statistics (total records, by language, by text type).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cache statistics</returns>
    Task<CacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an issue by its ID for cache validation purposes.
    /// Used to check if cached content is still fresh (compare CachedAt vs GitHubUpdatedAt).
    /// </summary>
    /// <param name="issueId">Internal issue ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Issue entity or null if not found</returns>
    Task<Issue?> GetIssueByIdAsync(int issueId, CancellationToken cancellationToken = default);

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
