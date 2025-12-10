using Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Models;

namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Services;

/// <summary>
/// Service for searching issues using semantic search.
/// </summary>
public interface IIssueSearchService
{
    /// <summary>
    /// Search issues using semantic similarity.
    /// </summary>
    /// <param name="query">Search query text.</param>
    /// <param name="state">Filter by issue state: "all", "open", or "closed".</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of results per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paginated search results.</returns>
    Task<SearchResultPage> SearchAsync(
        string query,
        string state = "all",
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);
}
