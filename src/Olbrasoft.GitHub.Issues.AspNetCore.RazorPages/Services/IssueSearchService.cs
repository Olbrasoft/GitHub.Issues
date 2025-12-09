using Microsoft.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Models;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Services;

public class IssueSearchService
{
    private readonly GitHubDbContext _dbContext;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<IssueSearchService> _logger;

    public IssueSearchService(
        GitHubDbContext dbContext,
        IEmbeddingService embeddingService,
        ILogger<IssueSearchService> logger)
    {
        _dbContext = dbContext;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    public async Task<List<IssueSearchResult>> SearchAsync(
        string query,
        string state = "all",
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new List<IssueSearchResult>();
        }

        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query, cancellationToken);

        if (queryEmbedding == null)
        {
            _logger.LogWarning("Failed to generate embedding for query: {Query}", query);
            return new List<IssueSearchResult>();
        }

        var issuesQuery = _dbContext.Issues
            .Include(i => i.Repository)
            .Where(i => i.TitleEmbedding != null);

        if (!string.Equals(state, "all", StringComparison.OrdinalIgnoreCase))
        {
            issuesQuery = issuesQuery.Where(i => i.State == state);
        }

        var results = await issuesQuery
            .OrderBy(i => i.TitleEmbedding!.CosineDistance(queryEmbedding))
            .Take(limit)
            .Select(i => new IssueSearchResult
            {
                Id = i.Id,
                Title = i.Title,
                State = i.State,
                HtmlUrl = i.HtmlUrl,
                RepositoryName = i.Repository.FullName,
                Similarity = 1 - i.TitleEmbedding!.CosineDistance(queryEmbedding)
            })
            .ToListAsync(cancellationToken);

        return results;
    }
}
