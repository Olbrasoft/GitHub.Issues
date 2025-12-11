using Microsoft.Extensions.Logging;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Business.Models;
using Olbrasoft.GitHub.Issues.Data.Dtos;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Services;
using Olbrasoft.GitHub.Issues.Data.Queries.IssueQueries;
using Olbrasoft.Mediation;
using static Olbrasoft.GitHub.Issues.Business.Services.IssueNumberParser;

namespace Olbrasoft.GitHub.Issues.Business.Services;

/// <summary>
/// Service for searching issues using semantic vector search.
/// </summary>
public class IssueSearchService : Service, IIssueSearchService
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IGitHubGraphQLClient _graphQLClient;
    private readonly ILogger<IssueSearchService> _logger;

    public IssueSearchService(
        IMediator mediator,
        IEmbeddingService embeddingService,
        IGitHubGraphQLClient graphQLClient,
        ILogger<IssueSearchService> logger)
        : base(mediator)
    {
        _embeddingService = embeddingService;
        _graphQLClient = graphQLClient;
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
        // Parse query for issue number patterns (e.g., #123, issue 123, repo#123)
        var parsedNumbers = Parse(query);
        var semanticQuery = GetSemanticQuery(query);
        var hasIssueNumbers = parsedNumbers.Count > 0;
        var hasSemanticQuery = !string.IsNullOrWhiteSpace(semanticQuery);

        var allResults = new List<IssueSearchResult>();
        var exactMatchIds = new HashSet<int>();

        // First: Find exact matches by issue number
        if (hasIssueNumbers)
        {
            var issueNumbers = parsedNumbers.Select(p => p.Number).ToList();
            var repoFilter = parsedNumbers.FirstOrDefault(p => p.RepositoryName != null)?.RepositoryName;

            var exactQuery = new IssuesByNumbersQuery(Mediator)
            {
                IssueNumbers = issueNumbers,
                RepositoryName = repoFilter,
                RepositoryIds = repositoryIds,
                State = state
            };

            var exactMatches = await exactQuery.ToResultAsync(cancellationToken);
            foreach (var dto in exactMatches)
            {
                var result = MapToSearchResult(dto, isExactMatch: true);
                allResults.Add(result);
                exactMatchIds.Add(dto.Id);
            }

            _logger.LogDebug("Found {Count} exact matches for issue numbers: {Numbers}",
                exactMatches.Count, string.Join(", ", issueNumbers));
        }

        // Second: Semantic search if there's query text
        if (hasSemanticQuery && semanticQuery != null)
        {
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(
                semanticQuery, EmbeddingInputType.Query, cancellationToken);

            if (queryEmbedding != null)
            {
                var searchQuery = new IssueSearchQuery(Mediator)
                {
                    QueryEmbedding = queryEmbedding,
                    State = state,
                    Page = 1, // Get more results to filter out duplicates
                    PageSize = pageSize + exactMatchIds.Count,
                    RepositoryIds = repositoryIds
                };

                var semanticPage = await searchQuery.ToResultAsync(cancellationToken);

                // Add semantic results, excluding already found exact matches
                foreach (var dto in semanticPage.Results.Where(d => !exactMatchIds.Contains(d.Id)))
                {
                    allResults.Add(MapToSearchResult(dto, isExactMatch: false));
                }
            }
            else
            {
                _logger.LogWarning("Failed to generate embedding for query: {Query}", semanticQuery);
            }
        }
        // Third: List issues without search (just browsing)
        else if (!hasIssueNumbers && repositoryIds is { Count: > 0 })
        {
            var listQuery = new IssueListQuery(Mediator)
            {
                State = state,
                Page = page,
                PageSize = pageSize,
                RepositoryIds = repositoryIds
            };

            var listPage = await listQuery.ToResultAsync(cancellationToken);
            foreach (var dto in listPage.Results)
            {
                allResults.Add(MapToSearchResult(dto, isExactMatch: false));
            }

            // For list queries, return paginated results directly
            await FetchBodiesAsync(allResults, cancellationToken);
            return new SearchResultPage
            {
                Results = allResults,
                TotalCount = listPage.TotalCount,
                Page = listPage.Page,
                PageSize = listPage.PageSize,
                TotalPages = listPage.TotalPages
            };
        }
        else if (!hasIssueNumbers && !hasSemanticQuery)
        {
            return new SearchResultPage();
        }

        // Apply pagination to combined results
        var totalCount = allResults.Count;
        var skip = (page - 1) * pageSize;
        var pagedResults = allResults.Skip(skip).Take(pageSize).ToList();

        await FetchBodiesAsync(pagedResults, cancellationToken);

        return new SearchResultPage
        {
            Results = pagedResults,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        };
    }

    private static IssueSearchResult MapToSearchResult(IssueSearchResultDto dto, bool isExactMatch)
    {
        var parts = dto.RepositoryFullName.Split('/');
        return new IssueSearchResult
        {
            Id = dto.Id,
            IssueNumber = dto.IssueNumber,
            Title = dto.Title,
            CzechTitle = dto.CzechTitle,
            IsOpen = dto.IsOpen,
            Url = dto.Url,
            RepositoryName = dto.RepositoryFullName,
            Owner = parts.Length == 2 ? parts[0] : string.Empty,
            RepoName = parts.Length == 2 ? parts[1] : string.Empty,
            Similarity = dto.Similarity,
            IsExactMatch = isExactMatch,
            Labels = dto.Labels
        };
    }

    private async Task FetchBodiesAsync(List<IssueSearchResult> results, CancellationToken cancellationToken)
    {
        if (results.Count == 0)
            return;

        var requests = results
            .Where(r => !string.IsNullOrEmpty(r.Owner) && !string.IsNullOrEmpty(r.RepoName))
            .Select(r => new IssueBodyRequest(r.Owner, r.RepoName, r.IssueNumber))
            .ToList();

        var bodies = await _graphQLClient.FetchBodiesAsync(requests, cancellationToken);

        foreach (var result in results)
        {
            var key = (result.Owner, result.RepoName, result.IssueNumber);
            if (bodies.TryGetValue(key, out var body))
            {
                result.Body = body;
            }
        }
    }
}
