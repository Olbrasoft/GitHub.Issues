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
/// Embedding dimensions configured per environment:
/// - Local (Ollama nomic-embed-text): 768 dimensions
/// - Azure (Cohere): 1024 dimensions
/// </remarks>
public class Issue
{
    public int Id { get; set; }
    public int RepositoryId { get; set; }
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsOpen { get; set; } = true;
    public string Url { get; set; } = string.Empty;
    public DateTimeOffset GitHubUpdatedAt { get; set; }
    public float[]? Embedding { get; set; }
    public DateTimeOffset SyncedAt { get; set; }

    // Cached AI-generated Czech summary
    public string? CzechSummary { get; set; }
    public string? SummaryProvider { get; set; }
    public DateTimeOffset? SummaryCachedAt { get; set; }

    // Cached AI-generated Czech title translation
    public string? CzechTitle { get; set; }
    public string? TitleTranslationProvider { get; set; }
    public DateTimeOffset? CzechTitleCachedAt { get; set; }

    public Repository Repository { get; set; } = null!;
    public ICollection<IssueLabel> IssueLabels { get; set; } = new List<IssueLabel>();
    public ICollection<IssueEvent> Events { get; set; } = new List<IssueEvent>();

    // Sub-issues hierarchy (1:N self-reference)
    public int? ParentIssueId { get; set; }
    public Issue? ParentIssue { get; set; }
    public ICollection<Issue> SubIssues { get; set; } = new List<Issue>();
}
