using static Olbrasoft.GitHub.Issues.Business.Services.IssueNumberParser;

namespace Olbrasoft.GitHub.Issues.Business.Strategies;

/// <summary>
/// Encapsulates all search parameters for issue searching.
/// </summary>
public class SearchCriteria
{
    /// <summary>
    /// The original search query string.
    /// </summary>
    public string Query { get; init; } = string.Empty;

    /// <summary>
    /// Parsed issue number references from the query (e.g., #123, issue 123).
    /// </summary>
    public IReadOnlyList<ParsedIssueNumber> ParsedIssueNumbers { get; init; } = [];

    /// <summary>
    /// The semantic query text after removing issue number patterns.
    /// </summary>
    public string? SemanticQuery { get; init; }

    /// <summary>
    /// Issue state filter: "open", "closed", or "all".
    /// </summary>
    public string State { get; init; } = "all";

    /// <summary>
    /// Current page number (1-based).
    /// </summary>
    public int Page { get; init; } = 1;

    /// <summary>
    /// Number of results per page.
    /// </summary>
    public int PageSize { get; init; } = 10;

    /// <summary>
    /// Optional repository IDs to filter by.
    /// </summary>
    public IReadOnlyList<int>? RepositoryIds { get; init; }

    /// <summary>
    /// Whether the query contains issue number references.
    /// </summary>
    public bool HasIssueNumbers => ParsedIssueNumbers.Count > 0;

    /// <summary>
    /// Whether the query has semantic text to search for.
    /// </summary>
    public bool HasSemanticQuery => !string.IsNullOrWhiteSpace(SemanticQuery);

    /// <summary>
    /// Whether the query targets specific repositories.
    /// </summary>
    public bool HasRepositoryFilter => RepositoryIds is { Count: > 0 };
}
