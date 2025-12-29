using Microsoft.Extensions.Logging;
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
public class RepositoryBrowseStrategy : SearchStrategyBase
{
    private readonly IMediator _mediator;

    public RepositoryBrowseStrategy(
        IMediator mediator,
        ILogger<RepositoryBrowseStrategy> logger,
        IOptions<AiSummarySettings> aiSummarySettings)
        : base(aiSummarySettings.Value.MaxLength, logger)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Lower priority - only used when no search terms are provided.
    /// </summary>
    public override int Priority => 50;

    public override bool CanHandle(SearchCriteria criteria)
    {
        // Only handle when there's no search criteria but we have repository filters
        return !criteria.HasIssueNumbers
               && !criteria.HasSemanticQuery
               && criteria.HasRepositoryFilter;
    }

    public override async Task<StrategySearchResult> ExecuteAsync(
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

        // Use base class mapping method to eliminate duplication
        // This strategy returns a terminal result with proper pagination
        var result = MapToStrategyResult(page.Results, existingResults, isExactMatch: false);
        result.TotalCount = page.TotalCount;
        result.IsTerminal = true;

        return result;
    }
}
