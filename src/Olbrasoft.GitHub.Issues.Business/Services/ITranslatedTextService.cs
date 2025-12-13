namespace Olbrasoft.GitHub.Issues.Business.Services;

/// <summary>
/// Service for retrieving translated/cached text with cache-first strategy.
/// Checks cache first, generates on miss, validates freshness via timestamps.
/// </summary>
public interface ITranslatedTextService
{
    /// <summary>
    /// Gets translated title from cache or generates new translation.
    /// For English, returns original title without caching.
    /// Validates cache freshness against Issue.GitHubUpdatedAt.
    /// </summary>
    /// <param name="issueId">The issue ID</param>
    /// <param name="languageId">The language LCID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Translated or original title</returns>
    Task<string> GetTitleAsync(int issueId, int languageId, CancellationToken ct = default);

    /// <summary>
    /// Gets list summary from cache or generates new via AI.
    /// Validates cache freshness against Issue.GitHubUpdatedAt.
    /// </summary>
    /// <param name="issueId">The issue ID</param>
    /// <param name="languageId">The language LCID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>AI-generated summary</returns>
    Task<string> GetListSummaryAsync(int issueId, int languageId, CancellationToken ct = default);

    /// <summary>
    /// Gets detail summary from cache or generates new via AI.
    /// Validates cache freshness against Issue.GitHubUpdatedAt.
    /// </summary>
    /// <param name="issueId">The issue ID</param>
    /// <param name="languageId">The language LCID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>AI-generated detail summary</returns>
    Task<string> GetDetailSummaryAsync(int issueId, int languageId, CancellationToken ct = default);

    /// <summary>
    /// Batch load for search results - more efficient than individual calls.
    /// Returns dictionary: IssueId -> (Title, ListSummary)
    /// </summary>
    /// <param name="issueIds">Issue IDs to load</param>
    /// <param name="languageId">The language LCID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Dictionary mapping issue IDs to their translated title and summary</returns>
    Task<Dictionary<int, (string Title, string Summary)>> GetForListAsync(
        IEnumerable<int> issueIds,
        int languageId,
        CancellationToken ct = default);
}
