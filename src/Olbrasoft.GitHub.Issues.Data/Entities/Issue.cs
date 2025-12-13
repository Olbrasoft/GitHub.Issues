namespace Olbrasoft.GitHub.Issues.Data.Entities;

/// <summary>
/// Issue entity with vector embedding for semantic search.
/// </summary>
/// <remarks>
/// ARCHITECTURAL DECISION: float[] type for cross-provider compatibility.
///
/// PostgreSQL: Uses pgvector extension with float[] mapped to vector type
/// SQL Server: Uses native VECTOR type via EFCore.SqlServer.VectorSearch
///
/// Embedding dimensions: 1024 (Cohere embed-multilingual-v3.0)
/// </remarks>
public class Issue
{
    public int Id { get; set; }
    public int RepositoryId { get; set; }
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsOpen { get; set; } = true;
    public bool IsDeleted { get; set; } = false;
    public string Url { get; set; } = string.Empty;
    public DateTimeOffset GitHubUpdatedAt { get; set; }

    /// <summary>
    /// Vector embedding for semantic search.
    /// </summary>
    /// <remarks>
    /// !!! CRITICAL: This property MUST be required (NOT NULL) !!!
    /// !!! DO NOT make this nullable - without embedding the record is useless !!!
    /// !!! We search by embedding - records without embedding cannot be found !!!
    /// </remarks>
    public float[] Embedding { get; set; } = [];

    public DateTimeOffset SyncedAt { get; set; }

    public Repository Repository { get; set; } = null!;
    public ICollection<IssueLabel> IssueLabels { get; set; } = new List<IssueLabel>();
    public ICollection<IssueEvent> Events { get; set; } = new List<IssueEvent>();
    public ICollection<CachedText> CachedTexts { get; set; } = [];

    // Sub-issues hierarchy (1:N self-reference)
    public int? ParentIssueId { get; set; }
    public Issue? ParentIssue { get; set; }
    public ICollection<Issue> SubIssues { get; set; } = new List<Issue>();
}
