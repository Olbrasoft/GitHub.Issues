namespace Olbrasoft.GitHub.Issues.Business.Models;

/// <summary>
/// Paginated search results for GitHub issues.
/// </summary>
/// <remarks>
/// ARCHITECTURAL NOTE: This model includes computed presentation properties (HasPreviousPage, HasNextPage).
/// This is intentional - the properties are simple, pure, and the model is already in the presentation layer.
/// See issue #58 for trade-off discussion.
/// </remarks>
public class SearchResultPage
{
    /// <summary>Search results for the current page.</summary>
    public List<IssueSearchResult> Results { get; set; } = new();

    /// <summary>Total number of matching issues across all pages.</summary>
    public int TotalCount { get; set; }

    /// <summary>Current page number (1-based).</summary>
    public int Page { get; set; } = 1;

    /// <summary>Number of results per page.</summary>
    public int PageSize { get; set; } = 10;

    /// <summary>Total number of pages.</summary>
    public int TotalPages { get; set; }

    /// <summary>
    /// Indicates whether there's a previous page available.
    /// Computed property for pagination UI convenience.
    /// </summary>
    public bool HasPreviousPage => Page > 1;

    /// <summary>
    /// Indicates whether there's a next page available.
    /// Computed property for pagination UI convenience.
    /// </summary>
    public bool HasNextPage => Page < TotalPages;
}
