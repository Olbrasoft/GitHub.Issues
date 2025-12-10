using Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Models;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Repositories;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Services;

namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Services;

public class IssueSearchService : IIssueSearchService
{
    private readonly IVectorSearchRepository _vectorSearchRepository;
    private readonly IEmbeddingService _embeddingService;
    private readonly IGitHubGraphQLClient _graphQLClient;
    private readonly ILogger<IssueSearchService> _logger;

    public IssueSearchService(
        IVectorSearchRepository vectorSearchRepository,
        IEmbeddingService embeddingService,
        IGitHubGraphQLClient graphQLClient,
        ILogger<IssueSearchService> logger)
    {
        _vectorSearchRepository = vectorSearchRepository;
        _embeddingService = embeddingService;
        _graphQLClient = graphQLClient;
        _logger = logger;
    }

    public async Task<SearchResultPage> SearchAsync(
        string query,
        string state = "all",
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new SearchResultPage();
        }

        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query, cancellationToken);

        if (queryEmbedding == null)
        {
            _logger.LogWarning("Failed to generate embedding for query: {Query}", query);
            return new SearchResultPage();
        }

        // Get total count for pagination
        var totalCount = await _vectorSearchRepository.GetTotalCountAsync(state, cancellationToken);

        // Get paginated results using provider-specific vector search
        var skip = (page - 1) * pageSize;
        var searchResults = await _vectorSearchRepository.SearchBySimilarityAsync(
            queryEmbedding, state, skip, pageSize, cancellationToken);

        // Map to IssueSearchResult and parse Owner/RepoName
        var results = searchResults.Select(r =>
        {
            var parts = r.RepositoryFullName.Split('/');
            return new IssueSearchResult
            {
                Id = r.Id,
                IssueNumber = r.IssueNumber,
                Title = r.Title,
                IsOpen = r.IsOpen,
                Url = r.Url,
                RepositoryName = r.RepositoryFullName,
                Owner = parts.Length == 2 ? parts[0] : string.Empty,
                RepoName = parts.Length == 2 ? parts[1] : string.Empty,
                Similarity = r.Similarity
            };
        }).ToList();

        // Fetch issue bodies from GitHub GraphQL API
        await FetchBodiesAsync(results, cancellationToken);

        return new SearchResultPage
        {
            Results = results,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
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
