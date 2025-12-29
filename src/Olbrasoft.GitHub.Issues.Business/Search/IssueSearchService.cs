using Microsoft.Extensions.Logging;
using Olbrasoft.GitHub.Issues.Business.Models;
using Olbrasoft.GitHub.Issues.Business.Search.Strategies;
using Olbrasoft.Mediation;
using static Olbrasoft.GitHub.Issues.Business.Detail.IssueNumberParser;

namespace Olbrasoft.GitHub.Issues.Business.Search;

/// <summary>
/// Service for searching issues using various strategies (semantic, exact match, text, browse).
/// Uses Strategy Pattern to support multiple search approaches.
/// </summary>
public class IssueSearchService : Service, IIssueSearchService
{
    private readonly IEnumerable<ISearchStrategy> _strategies;
    private readonly ILogger<IssueSearchService> _logger;

    public IssueSearchService(
        IMediator mediator,
        IEnumerable<ISearchStrategy> strategies,
        ILogger<IssueSearchService> logger)
        : base(mediator)
    {
        // Order strategies by priority (highest first)
        _strategies = strategies.OrderByDescending(s => s.Priority).ToList();
        _logger = logger;
    }

    public async Task<SearchResultPage> SearchAsync(
        string query,
        string state = "all",
        int page = 1,
        int pageSize = 10,
        IReadOnlyList<int>? repositoryIds = null,
        CancellationToken cancellationToken = default)
    {
        // Parse query and build criteria
        var parsedNumbers = Parse(query);
        var semanticQuery = GetSemanticQuery(query);

        var criteria = new SearchCriteria
        {
            Query = query,
            ParsedIssueNumbers = parsedNumbers,
            SemanticQuery = semanticQuery,
            State = state,
            Page = page,
            PageSize = pageSize,
            RepositoryIds = repositoryIds
        };

        // Check if any strategy can handle this search
        var applicableStrategies = _strategies.Where(s => s.CanHandle(criteria)).ToList();

        if (applicableStrategies.Count == 0)
        {
            _logger.LogDebug("No applicable search strategy for query: {Query}", query);
            return new SearchResultPage();
        }

        // Execute strategies in priority order, accumulating results
        var allResults = new List<IssueSearchResult>();
        var allFoundIds = new HashSet<int>();

        foreach (var strategy in applicableStrategies)
        {
            var result = await strategy.ExecuteAsync(criteria, allFoundIds, cancellationToken);

            allResults.AddRange(result.Results);
            foreach (var id in result.FoundIds)
            {
                allFoundIds.Add(id);
            }

            // If strategy returns a terminal result, use it directly
            if (result.IsTerminal)
            {
                return new SearchResultPage
                {
                    Results = result.Results,
                    TotalCount = result.TotalCount ?? result.Results.Count,
                    Page = criteria.Page,
                    PageSize = criteria.PageSize,
                    TotalPages = (int)Math.Ceiling((double)(result.TotalCount ?? result.Results.Count) / criteria.PageSize)
                };
            }
        }

        // Apply pagination to combined results
        var totalCount = allResults.Count;
        var skip = (page - 1) * pageSize;
        var pagedResults = allResults.Skip(skip).Take(pageSize).ToList();

        return new SearchResultPage
        {
            Results = pagedResults,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        };
    }
}
