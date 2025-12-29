using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Data.Queries.IssueQueries;
using Olbrasoft.Text.Transformation.Abstractions;
using Olbrasoft.Mediation;

namespace Olbrasoft.GitHub.Issues.Business.Search.Strategies;

/// <summary>
/// Strategy for semantic vector search using embeddings.
/// Falls back to text search if embedding generation fails.
/// </summary>
public class SemanticSearchStrategy : ISearchStrategy
{
    private readonly IMediator _mediator;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<SemanticSearchStrategy> _logger;
    private readonly int _previewMaxLength;

    public SemanticSearchStrategy(
        IMediator mediator,
        IEmbeddingService embeddingService,
        ILogger<SemanticSearchStrategy> logger,
        IOptions<AiSummarySettings> aiSummarySettings)
    {
        _mediator = mediator;
        _embeddingService = embeddingService;
        _logger = logger;
        _previewMaxLength = aiSummarySettings.Value.MaxLength;
    }

    /// <summary>
    /// Medium-high priority - semantic search is preferred after exact matches.
    /// </summary>
    public int Priority => 80;

    public bool CanHandle(SearchCriteria criteria)
    {
        return criteria.HasSemanticQuery;
    }

    public async Task<StrategySearchResult> ExecuteAsync(
        SearchCriteria criteria,
        IReadOnlySet<int> existingResults,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(criteria.SemanticQuery))
        {
            return new StrategySearchResult();
        }

        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(
            criteria.SemanticQuery, EmbeddingInputType.Query, cancellationToken);

        if (queryEmbedding != null)
        {
            return await ExecuteVectorSearchAsync(criteria, existingResults, queryEmbedding, cancellationToken);
        }

        // Fallback to text search when embedding is unavailable
        _logger.LogWarning("Embedding unavailable, falling back to text search for query: {Query}", criteria.SemanticQuery);
        return await ExecuteTextSearchAsync(criteria, existingResults, cancellationToken);
    }

    private async Task<StrategySearchResult> ExecuteVectorSearchAsync(
        SearchCriteria criteria,
        IReadOnlySet<int> existingResults,
        float[] queryEmbedding,
        CancellationToken cancellationToken)
    {
        var query = new IssueSearchQuery(_mediator)
        {
            QueryEmbedding = queryEmbedding,
            State = criteria.State,
            Page = 1,
            PageSize = criteria.PageSize + existingResults.Count,
            RepositoryIds = criteria.RepositoryIds
        };

        var page = await query.ToResultAsync(cancellationToken);

        var result = new StrategySearchResult
        {
            Results = [],
            FoundIds = [],
            TotalCount = page.TotalCount
        };

        foreach (var dto in page.Results.Where(d => !existingResults.Contains(d.Id)))
        {
            var searchResult = SearchResultMapper.MapToSearchResult(dto, isExactMatch: false, _previewMaxLength);
            result.Results.Add(searchResult);
            result.FoundIds.Add(dto.Id);
        }

        _logger.LogDebug("SemanticSearch found {Count} results for query: {Query}",
            result.Results.Count, criteria.SemanticQuery);

        return result;
    }

    private async Task<StrategySearchResult> ExecuteTextSearchAsync(
        SearchCriteria criteria,
        IReadOnlySet<int> existingResults,
        CancellationToken cancellationToken)
    {
        var query = new IssueTextSearchQuery(_mediator)
        {
            SearchText = criteria.SemanticQuery!,
            State = criteria.State,
            Page = 1,
            PageSize = criteria.PageSize + existingResults.Count,
            RepositoryIds = criteria.RepositoryIds
        };

        var page = await query.ToResultAsync(cancellationToken);

        var result = new StrategySearchResult
        {
            Results = [],
            FoundIds = [],
            TotalCount = page.TotalCount
        };

        foreach (var dto in page.Results.Where(d => !existingResults.Contains(d.Id)))
        {
            var searchResult = SearchResultMapper.MapToSearchResult(dto, isExactMatch: false, _previewMaxLength);
            result.Results.Add(searchResult);
            result.FoundIds.Add(dto.Id);
        }

        _logger.LogInformation("TextSearchFallback returned {Count} results for query: {Query}",
            page.TotalCount, criteria.SemanticQuery);

        return result;
    }
}
