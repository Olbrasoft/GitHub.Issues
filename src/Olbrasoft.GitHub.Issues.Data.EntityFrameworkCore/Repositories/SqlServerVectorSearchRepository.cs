using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Repositories;

/// <summary>
/// SQL Server implementation of vector search using native VECTOR type.
/// Uses raw SQL with VECTOR_DISTANCE() function (Azure SQL GA since 2025).
/// </summary>
public class SqlServerVectorSearchRepository : IVectorSearchRepository
{
    private readonly GitHubDbContext _dbContext;

    public SqlServerVectorSearchRepository(GitHubDbContext dbContext)
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
        var stateFilter = GetStateFilter(state);
        var queryVector = VectorToSqlString(queryEmbedding);

        // Use raw SQL with VECTOR_DISTANCE function
        // Note: Azure SQL uses VECTOR_DISTANCE('cosine', v1, v2) which returns distance (0-2 for cosine)
        var sql = $@"
            SELECT
                i.id AS Id,
                i.number AS IssueNumber,
                i.title AS Title,
                i.is_open AS IsOpen,
                i.url AS Url,
                r.full_name AS RepositoryFullName,
                1 - VECTOR_DISTANCE('cosine', i.embedding, CAST({queryVector} AS VECTOR(768))) AS Similarity
            FROM issues i
            INNER JOIN repositories r ON i.repository_id = r.id
            WHERE i.embedding IS NOT NULL
            {stateFilter}
            ORDER BY VECTOR_DISTANCE('cosine', i.embedding, CAST({queryVector} AS VECTOR(768)))
            OFFSET {skip} ROWS FETCH NEXT {take} ROWS ONLY";

        return await _dbContext.Database
            .SqlQueryRaw<VectorSearchResult>(sql)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetTotalCountAsync(string state, CancellationToken cancellationToken = default)
    {
        var stateFilter = GetStateFilter(state);

        var sql = $@"
            SELECT COUNT(*)
            FROM issues i
            WHERE i.embedding IS NOT NULL
            {stateFilter}";

        return await _dbContext.Database
            .SqlQueryRaw<int>(sql)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string GetStateFilter(string state)
    {
        if (string.Equals(state, "open", StringComparison.OrdinalIgnoreCase))
            return "AND i.is_open = 1";
        if (string.Equals(state, "closed", StringComparison.OrdinalIgnoreCase))
            return "AND i.is_open = 0";
        return string.Empty;
    }

    /// <summary>
    /// Converts a Vector to SQL Server VECTOR literal format.
    /// Format: '[0.1, 0.2, 0.3, ...]'
    /// </summary>
    private static string VectorToSqlString(Vector vector)
    {
        var values = vector.ToArray();
        return $"'[{string.Join(", ", values.Select(v => v.ToString(System.Globalization.CultureInfo.InvariantCulture)))}]'";
    }
}
