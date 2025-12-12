using Olbrasoft.GitHub.Issues.Business.Models;

namespace Olbrasoft.GitHub.Issues.Business.Strategies;

/// <summary>
/// Strategy interface for issue search operations.
/// Implementations handle different search scenarios (exact match, semantic, text, browsing).
/// </summary>
public interface ISearchStrategy
{
    /// <summary>
    /// Determines whether this strategy can handle the given search criteria.
    /// </summary>
    /// <param name="criteria">The search parameters.</param>
    /// <returns>True if this strategy should be used for the criteria.</returns>
    bool CanHandle(SearchCriteria criteria);

    /// <summary>
    /// Priority for strategy selection. Higher values are checked first.
    /// Used when multiple strategies can handle the same criteria.
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Executes the search using this strategy's approach.
    /// </summary>
    /// <param name="criteria">The search parameters.</param>
    /// <param name="existingResults">Already found results (e.g., exact matches) to exclude.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Search results from this strategy.</returns>
    Task<StrategySearchResult> ExecuteAsync(
        SearchCriteria criteria,
        IReadOnlySet<int> existingResults,
        CancellationToken cancellationToken);
}

/// <summary>
/// Result from a search strategy execution.
/// </summary>
public class StrategySearchResult
{
    /// <summary>
    /// The search results found by this strategy.
    /// </summary>
    public List<IssueSearchResult> Results { get; init; } = [];

    /// <summary>
    /// IDs of issues found (for deduplication in subsequent strategies).
    /// </summary>
    public HashSet<int> FoundIds { get; init; } = [];

    /// <summary>
    /// Total count if available (for pagination).
    /// </summary>
    public int? TotalCount { get; init; }

    /// <summary>
    /// Whether this result should be returned directly (bypassing other strategies).
    /// </summary>
    public bool IsTerminal { get; init; }
}
