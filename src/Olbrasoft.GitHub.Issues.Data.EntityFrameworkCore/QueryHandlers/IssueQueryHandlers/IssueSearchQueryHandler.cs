using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Olbrasoft.GitHub.Issues.Data.Dtos;
using Olbrasoft.GitHub.Issues.Data.Entities;
using Olbrasoft.GitHub.Issues.Data.Queries.IssueQueries;
using Pgvector.EntityFrameworkCore;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.QueryHandlers.IssueQueryHandlers;

/// <summary>
/// Handles issue search queries using vector similarity.
/// Supports both PostgreSQL (pgvector CosineDistance) and SQL Server (VECTOR_DISTANCE raw SQL).
/// </summary>
public class IssueSearchQueryHandler : GitHubDbQueryHandler<Issue, IssueSearchQuery, IssueSearchPageDto>
{
    private readonly DatabaseSettings _settings;

    public IssueSearchQueryHandler(GitHubDbContext context, IOptions<DatabaseSettings> settings) : base(context)
    {
        _settings = settings.Value;
    }

    protected override async Task<IssueSearchPageDto> GetResultToHandleAsync(
        IssueSearchQuery query, CancellationToken token)
    {
        return _settings.Provider switch
        {
            DatabaseProvider.SqlServer => await ExecuteSqlServerSearchAsync(query, token),
            _ => await ExecutePostgreSqlSearchAsync(query, token)
        };
    }

    /// <summary>
    /// PostgreSQL implementation using pgvector CosineDistance().
    /// </summary>
    private async Task<IssueSearchPageDto> ExecutePostgreSqlSearchAsync(
        IssueSearchQuery query, CancellationToken token)
    {
        var baseQuery = BuildBaseQuery(query.State, query.RepositoryIds);

        var totalCount = await baseQuery.CountAsync(token);
        var skip = (query.Page - 1) * query.PageSize;

        var results = await baseQuery
            .OrderBy(i => i.Embedding!.CosineDistance(query.QueryEmbedding))
            .Skip(skip)
            .Take(query.PageSize)
            .Select(i => new IssueSearchResultDto
            {
                Id = i.Id,
                IssueNumber = i.Number,
                Title = i.Title,
                IsOpen = i.IsOpen,
                Url = i.Url,
                RepositoryFullName = i.Repository.FullName,
                Similarity = 1 - i.Embedding!.CosineDistance(query.QueryEmbedding)
            })
            .ToListAsync(token);

        return new IssueSearchPageDto
        {
            Results = results,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / query.PageSize)
        };
    }

    /// <summary>
    /// SQL Server implementation using VECTOR_DISTANCE() with raw SQL.
    /// Embeddings are stored as varbinary(max) and cast to VECTOR for distance calculation.
    /// </summary>
    private async Task<IssueSearchPageDto> ExecuteSqlServerSearchAsync(
        IssueSearchQuery query, CancellationToken token)
    {
        // Get total count using LINQ (works with any provider)
        var countQuery = BuildBaseQuery(query.State, query.RepositoryIds);
        var totalCount = await countQuery.CountAsync(token);

        // Build WHERE clause conditions for raw SQL
        var stateFilter = query.State?.ToLowerInvariant() switch
        {
            "open" => "AND i.IsOpen = 1",
            "closed" => "AND i.IsOpen = 0",
            _ => ""
        };

        var repoFilter = "";
        if (query.RepositoryIds is { Count: > 0 })
        {
            var repoIds = string.Join(",", query.RepositoryIds);
            repoFilter = $"AND i.RepositoryId IN ({repoIds})";
        }

        // Convert query embedding to binary format for SQL Server
        var queryEmbeddingBytes = VectorToBytes(query.QueryEmbedding.ToArray());
        var dimensions = query.QueryEmbedding.ToArray().Length;

        var skip = (query.Page - 1) * query.PageSize;

        // Execute vector search with raw SQL using VECTOR_DISTANCE
        var searchSql = $@"
            SELECT
                i.Id,
                i.Number AS IssueNumber,
                i.Title,
                i.IsOpen,
                i.Url,
                r.FullName AS RepositoryFullName,
                1.0 - VECTOR_DISTANCE('cosine', CAST(i.Embedding AS VECTOR({dimensions})), CAST(@p0 AS VECTOR({dimensions}))) AS Similarity
            FROM Issues i
            INNER JOIN Repositories r ON i.RepositoryId = r.Id
            WHERE i.Embedding IS NOT NULL
            {stateFilter}
            {repoFilter}
            ORDER BY VECTOR_DISTANCE('cosine', CAST(i.Embedding AS VECTOR({dimensions})), CAST(@p0 AS VECTOR({dimensions}))) ASC
            OFFSET {skip} ROWS FETCH NEXT {query.PageSize} ROWS ONLY";

        var results = await Context.Database
            .SqlQueryRaw<IssueSearchResultDto>(searchSql, queryEmbeddingBytes)
            .ToListAsync(token);

        return new IssueSearchPageDto
        {
            Results = results,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / query.PageSize)
        };
    }

    /// <summary>
    /// Converts float array to binary format for SQL Server VECTOR type.
    /// </summary>
    private static byte[] VectorToBytes(float[] vector)
    {
        var bytes = new byte[vector.Length * sizeof(float)];
        for (int i = 0; i < vector.Length; i++)
        {
            BitConverter.GetBytes(vector[i]).CopyTo(bytes, i * sizeof(float));
        }
        return bytes;
    }

    private IQueryable<Issue> BuildBaseQuery(string state, IReadOnlyList<int>? repositoryIds)
    {
        var query = Entities
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

        if (repositoryIds is { Count: > 0 })
        {
            query = query.Where(i => repositoryIds.Contains(i.RepositoryId));
        }

        return query;
    }
}
