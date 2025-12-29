using Microsoft.Extensions.Logging;
using Olbrasoft.GitHub.Issues.Business.Models;
using Olbrasoft.GitHub.Issues.Data.Dtos;

namespace Olbrasoft.GitHub.Issues.Business.Strategies;

/// <summary>
/// Base class for search strategies providing shared mapping logic.
/// Eliminates code duplication across strategy implementations (DRY principle - issue #287).
/// </summary>
public abstract class SearchStrategyBase : ISearchStrategy
{
    protected readonly int PreviewMaxLength;
    protected readonly ILogger Logger;

    protected SearchStrategyBase(int previewMaxLength, ILogger logger)
    {
        PreviewMaxLength = previewMaxLength;
        Logger = logger;
    }

    /// <inheritdoc />
    public abstract int Priority { get; }

    /// <inheritdoc />
    public abstract bool CanHandle(SearchCriteria criteria);

    /// <inheritdoc />
    public abstract Task<StrategySearchResult> ExecuteAsync(
        SearchCriteria criteria,
        IReadOnlySet<int> existingResults,
        CancellationToken cancellationToken);

    /// <summary>
    /// Maps a single DTO to a search result.
    /// Shared logic extracted from all strategies.
    /// </summary>
    /// <param name="dto">The issue DTO from query</param>
    /// <param name="isExactMatch">Whether this is an exact match result</param>
    /// <returns>Mapped search result</returns>
    protected IssueSearchResult MapToResult(IssueSearchResultDto dto, bool isExactMatch)
    {
        return SearchResultMapper.MapToSearchResult(dto, isExactMatch, PreviewMaxLength);
    }

    /// <summary>
    /// Maps DTOs to search results, filtering out existing results and tracking found IDs.
    /// Common pattern across all strategies - extracted to eliminate duplication.
    /// </summary>
    /// <param name="dtos">DTOs from database query</param>
    /// <param name="existingResults">Already found issue IDs to exclude</param>
    /// <param name="isExactMatch">Whether these are exact match results</param>
    /// <returns>Strategy search result with results and found IDs</returns>
    protected StrategySearchResult MapToStrategyResult(
        IEnumerable<IssueSearchResultDto> dtos,
        IReadOnlySet<int> existingResults,
        bool isExactMatch)
    {
        var result = new StrategySearchResult
        {
            Results = [],
            FoundIds = []
        };

        foreach (var dto in dtos.Where(d => !existingResults.Contains(d.Id)))
        {
            var searchResult = MapToResult(dto, isExactMatch);
            result.Results.Add(searchResult);
            result.FoundIds.Add(dto.Id);
        }

        return result;
    }

    /// <summary>
    /// Maps DTOs from a paged result to strategy search result.
    /// Includes total count for pagination.
    /// </summary>
    /// <param name="page">Paged query result</param>
    /// <param name="existingResults">Already found issue IDs to exclude</param>
    /// <param name="isExactMatch">Whether these are exact match results</param>
    /// <param name="isTerminal">Whether this result should bypass other strategies</param>
    /// <returns>Strategy search result with results, found IDs, and total count</returns>
    protected StrategySearchResult MapPageToStrategyResult<TPage>(
        TPage page,
        IReadOnlySet<int> existingResults,
        bool isExactMatch,
        bool isTerminal = false)
        where TPage : IPagedResult<IssueSearchResultDto>
    {
        var result = MapToStrategyResult(page.Results, existingResults, isExactMatch);
        result.TotalCount = page.TotalCount;
        result.IsTerminal = isTerminal;
        return result;
    }
}

/// <summary>
/// Marker interface for paged results to enable generic mapping.
/// </summary>
public interface IPagedResult<out T>
{
    IEnumerable<T> Results { get; }
    int TotalCount { get; }
}
