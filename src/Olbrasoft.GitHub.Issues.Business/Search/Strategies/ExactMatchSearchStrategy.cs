using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Queries.IssueQueries;
using Olbrasoft.Mediation;

namespace Olbrasoft.GitHub.Issues.Business.Search.Strategies;

/// <summary>
/// Strategy for searching issues by exact issue number references.
/// Handles patterns like #123, issue 123, repo#123.
/// </summary>
public class ExactMatchSearchStrategy : ISearchStrategy
{
    private readonly IMediator _mediator;
    private readonly ILogger<ExactMatchSearchStrategy> _logger;
    private readonly int _previewMaxLength;

    public ExactMatchSearchStrategy(
        IMediator mediator,
        ILogger<ExactMatchSearchStrategy> logger,
        IOptions<AiSummarySettings> aiSummarySettings)
    {
        _mediator = mediator;
        _logger = logger;
        _previewMaxLength = aiSummarySettings.Value.MaxLength;
    }

    /// <summary>
    /// Highest priority - exact matches should always be found first.
    /// </summary>
    public int Priority => 100;

    public bool CanHandle(SearchCriteria criteria)
    {
        return criteria.HasIssueNumbers;
    }

    public async Task<StrategySearchResult> ExecuteAsync(
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

        var result = new StrategySearchResult
        {
            Results = [],
            FoundIds = []
        };

        foreach (var dto in matches.Where(d => !existingResults.Contains(d.Id)))
        {
            var searchResult = SearchResultMapper.MapToSearchResult(dto, isExactMatch: true, _previewMaxLength);
            result.Results.Add(searchResult);
            result.FoundIds.Add(dto.Id);
        }

        _logger.LogDebug("ExactMatchStrategy found {Count} matches for issue numbers: {Numbers}",
            result.Results.Count, string.Join(", ", issueNumbers));

        return result;
    }
}
