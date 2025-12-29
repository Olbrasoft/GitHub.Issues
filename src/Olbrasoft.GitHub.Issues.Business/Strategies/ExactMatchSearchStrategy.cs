using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Queries.IssueQueries;
using Olbrasoft.Mediation;

namespace Olbrasoft.GitHub.Issues.Business.Strategies;

/// <summary>
/// Strategy for searching issues by exact issue number references.
/// Handles patterns like #123, issue 123, repo#123.
/// </summary>
public class ExactMatchSearchStrategy : SearchStrategyBase
{
    private readonly IMediator _mediator;

    public ExactMatchSearchStrategy(
        IMediator mediator,
        ILogger<ExactMatchSearchStrategy> logger,
        IOptions<AiSummarySettings> aiSummarySettings)
        : base(aiSummarySettings.Value.MaxLength, logger)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Highest priority - exact matches should always be found first.
    /// </summary>
    public override int Priority => 100;

    public override bool CanHandle(SearchCriteria criteria)
    {
        return criteria.HasIssueNumbers;
    }

    public override async Task<StrategySearchResult> ExecuteAsync(
        SearchCriteria criteria,
        IReadOnlySet<int> existingResults,
        CancellationToken cancellationToken)
    {
        var issueNumbers = criteria.ParsedIssueNumbers.Select(p => p.Number).ToList();
        var repoFilter = criteria.ParsedIssueNumbers.FirstOrDefault(p => p.RepositoryName != null)?.RepositoryName;

        var query = new IssuesByNumbersQuery(_mediator)
        {
            IssueNumbers = issueNumbers,
            RepositoryName = repoFilter,
            RepositoryIds = criteria.RepositoryIds,
            State = criteria.State
        };

        var matches = await query.ToResultAsync(cancellationToken);

        // Use base class mapping method to eliminate duplication
        var result = MapToStrategyResult(matches, existingResults, isExactMatch: true);

        Logger.LogDebug("ExactMatchStrategy found {Count} matches for issue numbers: {Numbers}",
            result.Results.Count, string.Join(", ", issueNumbers));

        return result;
    }
}
