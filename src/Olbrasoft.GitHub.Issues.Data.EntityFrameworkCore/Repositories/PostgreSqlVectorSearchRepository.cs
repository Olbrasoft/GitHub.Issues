using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Repositories;

/// <summary>
/// PostgreSQL implementation of vector search using pgvector extension.
/// Uses LINQ with CosineDistance() extension method.
/// </summary>
public class PostgreSqlVectorSearchRepository : IVectorSearchRepository
{
    private readonly GitHubDbContext _dbContext;

    public PostgreSqlVectorSearchRepository(GitHubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<VectorSearchResult>> SearchBySimilarityAsync(
        Vector queryEmbedding,
        string state,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = BuildBaseQuery(state);

        return await query
            .OrderBy(i => i.Embedding!.CosineDistance(queryEmbedding))
            .Skip(skip)
            .Take(take)
            .Select(i => new VectorSearchResult
            {
                Id = i.Id,
                IssueNumber = i.Number,
                Title = i.Title,
                IsOpen = i.IsOpen,
                Url = i.Url,
                RepositoryFullName = i.Repository.FullName,
                Similarity = 1 - i.Embedding!.CosineDistance(queryEmbedding)
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetTotalCountAsync(string state, CancellationToken cancellationToken = default)
    {
        return await BuildBaseQuery(state).CountAsync(cancellationToken);
    }

    private IQueryable<Data.Entities.Issue> BuildBaseQuery(string state)
    {
        var query = _dbContext.Issues
            .Include(i => i.Repository)
            .Where(i => i.Embedding != null)
            .AsQueryable();

        if (string.Equals(state, "open", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(i => i.IsOpen);
        }
        else if (string.Equals(state, "closed", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(i => !i.IsOpen);
        }

        return query;
    }
}
