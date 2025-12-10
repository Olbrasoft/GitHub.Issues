using Microsoft.Extensions.Logging;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Business.Models;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Services;
using Olbrasoft.GitHub.Issues.Data.Queries.IssueQueries;
using Olbrasoft.Mediation;

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
        if (string.IsNullOrWhiteSpace(query))
        {
            return new SearchResultPage();
        }

        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query, EmbeddingInputType.Query, cancellationToken);

        if (queryEmbedding == null)
        {
            _logger.LogWarning("Failed to generate embedding for query: {Query}", query);
            return new SearchResultPage();
        }

        // Execute CQRS query for vector search using Mediator
        var searchQuery = new IssueSearchQuery(Mediator)
        {
            QueryEmbedding = queryEmbedding,
            State = state,
            Page = page,
            PageSize = pageSize,
            RepositoryIds = repositoryIds
        };

        var searchPage = await searchQuery.ToResultAsync(cancellationToken);

        // Map DTOs to presentation models
        var results = searchPage.Results.Select(dto =>
        {
            var parts = dto.RepositoryFullName.Split('/');
            return new IssueSearchResult
            {
                Id = dto.Id,
                IssueNumber = dto.IssueNumber,
                Title = dto.Title,
                IsOpen = dto.IsOpen,
                Url = dto.Url,
                RepositoryName = dto.RepositoryFullName,
                Owner = parts.Length == 2 ? parts[0] : string.Empty,
                RepoName = parts.Length == 2 ? parts[1] : string.Empty,
                Similarity = dto.Similarity
            };
        }).ToList();

        // Fetch issue bodies from GitHub GraphQL API
        await FetchBodiesAsync(results, cancellationToken);

        return new SearchResultPage
        {
            Results = results,
            TotalCount = searchPage.TotalCount,
            Page = searchPage.Page,
            PageSize = searchPage.PageSize,
            TotalPages = searchPage.TotalPages
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
