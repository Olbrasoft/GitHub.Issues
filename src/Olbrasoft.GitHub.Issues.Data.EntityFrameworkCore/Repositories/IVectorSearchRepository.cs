using Pgvector;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Repositories;

/// <summary>
/// Repository for vector-based semantic search operations.
/// Abstracts provider-specific vector distance calculations.
/// </summary>
public interface IVectorSearchRepository
{
    /// <summary>
    /// Searches issues by vector similarity using cosine distance.
    /// </summary>
    /// <param name="queryEmbedding">The query embedding vector.</param>
    /// <param name="state">Filter by state: "open", "closed", or "all".</param>
    /// <param name="skip">Number of results to skip (for pagination).</param>
    /// <param name="take">Number of results to take.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of issue search results with similarity scores.</returns>
    Task<List<VectorSearchResult>> SearchBySimilarityAsync(
        Vector queryEmbedding,
        string state,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets total count of issues matching the filter.
    /// </summary>
    Task<int> GetTotalCountAsync(string state, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result from vector similarity search.
/// </summary>
public class VectorSearchResult
{
    public int Id { get; set; }
    public int IssueNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsOpen { get; set; }
    public string Url { get; set; } = string.Empty;
    public string RepositoryFullName { get; set; } = string.Empty;
    public double Similarity { get; set; }
}
