using Microsoft.Extensions.Options;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Business.Models;
using Olbrasoft.GitHub.Issues.Data.Queries.IssueQueries;
using Olbrasoft.Mediation;

namespace Olbrasoft.GitHub.Issues.Business.Strategies;

/// <summary>
/// Strategy for browsing issues in specific repositories without a search query.
/// Used when user selects repositories but doesn't enter search text.
/// </summary>
public class RepositoryBrowseStrategy : ISearchStrategy
{
    private readonly IMediator _mediator;
    private readonly int _previewMaxLength;

    public RepositoryBrowseStrategy(
        IMediator mediator,
        IOptions<AiSummarySettings> aiSummarySettings)
    {
        _mediator = mediator;
        _previewMaxLength = aiSummarySettings.Value.MaxLength;
    }

    /// <summary>
    /// Lower priority - only used when no search terms are provided.
    /// </summary>
    public int Priority => 50;

    public bool CanHandle(SearchCriteria criteria)
    {
        // Only handle when there's no search criteria but we have repository filters
        return !criteria.HasIssueNumbers
               && !criteria.HasSemanticQuery
               && criteria.HasRepositoryFilter;
    }

    public async Task<StrategySearchResult> ExecuteAsync(
        SearchCriteria criteria,
        IReadOnlySet<int> existingResults,
        CancellationToken cancellationToken)
    {
        var query = new IssueListQuery(_mediator)
        {
            State = criteria.State,
            Page = criteria.Page,
            PageSize = criteria.PageSize,
            RepositoryIds = criteria.RepositoryIds
        };

        var page = await query.ToResultAsync(cancellationToken);

        var results = new List<IssueSearchResult>();
        var foundIds = new HashSet<int>();

        foreach (var dto in page.Results.Where(d => !existingResults.Contains(d.Id)))
        {
            var searchResult = SearchResultMapper.MapToSearchResult(dto, isExactMatch: false, _previewMaxLength);
            results.Add(searchResult);
            foundIds.Add(dto.Id);
        }

        // This strategy returns a terminal result with proper pagination
        return new StrategySearchResult
        {
            Results = results,
            FoundIds = foundIds,
            TotalCount = page.TotalCount,
            IsTerminal = true
        };
    }
}
