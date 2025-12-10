using Microsoft.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Models;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Services;
using Pgvector.EntityFrameworkCore;

namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Services;

public class IssueSearchService : IIssueSearchService
{
    private readonly GitHubDbContext _dbContext;
    private readonly IEmbeddingService _embeddingService;
    private readonly IGitHubGraphQLClient _graphQLClient;
    private readonly ILogger<IssueSearchService> _logger;

    public IssueSearchService(
        GitHubDbContext dbContext,
        IEmbeddingService embeddingService,
        IGitHubGraphQLClient graphQLClient,
        ILogger<IssueSearchService> logger)
    {
        _dbContext = dbContext;
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

        var issuesQuery = _dbContext.Issues
            .Include(i => i.Repository)
            .Where(i => i.Embedding != null)
            .AsQueryable();

        if (string.Equals(state, "open", StringComparison.OrdinalIgnoreCase))
        {
            issuesQuery = issuesQuery.Where(i => i.IsOpen);
        }
        else if (string.Equals(state, "closed", StringComparison.OrdinalIgnoreCase))
        {
            issuesQuery = issuesQuery.Where(i => !i.IsOpen);
        }

        // Get total count for pagination
        var totalCount = await issuesQuery.CountAsync(cancellationToken);

        // Get paginated results
        var skip = (page - 1) * pageSize;
        var results = await issuesQuery
            .OrderBy(i => i.Embedding!.CosineDistance(queryEmbedding))
            .Skip(skip)
            .Take(pageSize)
            .Select(i => new IssueSearchResult
            {
                Id = i.Id,
                IssueNumber = i.Number,
                Title = i.Title,
                IsOpen = i.IsOpen,
                Url = i.Url,
                RepositoryName = i.Repository.FullName,
                Similarity = 1 - i.Embedding!.CosineDistance(queryEmbedding)
            })
            .ToListAsync(cancellationToken);

        // Parse Owner/RepoName from FullName and fetch bodies
        foreach (var result in results)
        {
            var parts = result.RepositoryName.Split('/');
            if (parts.Length == 2)
            {
                result.Owner = parts[0];
                result.RepoName = parts[1];
            }
        }

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
