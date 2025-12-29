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
    /// Saves a new cached text entry to the database.
    /// Handles duplicate key exceptions gracefully (concurrent cache inserts).
    /// </summary>
    /// <param name="cachedText">Cached text entity to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SaveAsync(CachedText cachedText, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a cached text entry from the database.
    /// Used when cache is stale (issue was updated after text was cached).
    /// </summary>
    /// <param name="cachedText">Cached text entity to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteAsync(CachedText cachedText, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an issue by its ID for cache validation purposes.
    /// Used to check if cached content is still fresh (compare CachedAt vs GitHubUpdatedAt).
    /// </summary>
    /// <param name="issueId">Internal issue ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Issue entity or null if not found</returns>
    Task<Issue?> GetIssueByIdAsync(int issueId, CancellationToken cancellationToken = default);
}
