using Pgvector;

namespace Olbrasoft.GitHub.Issues.Data.Entities;

/// <summary>
/// Issue entity with vector embedding for semantic search.
/// </summary>
/// <remarks>
/// ARCHITECTURAL DECISION: Vector type from Pgvector package is used directly in entity.
///
/// Alternatives considered (see issue #56):
/// - float[] with EF value conversion: Won't work - CosineDistance() extension requires Vector type
/// - Separate domain/persistence entities: Over-engineered for this use case
///
/// Trade-off: Accept infrastructure dependency in Data layer for:
/// - Full LINQ support with CosineDistance(), L2Distance(), etc.
/// - Type safety and proper vector dimension handling
/// - Minimal package footprint (~100KB, no external dependencies)
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
    public Vector Embedding { get; set; } = null!;
    public DateTimeOffset SyncedAt { get; set; }

    public Repository Repository { get; set; } = null!;
    public ICollection<IssueLabel> IssueLabels { get; set; } = new List<IssueLabel>();
    public ICollection<IssueEvent> Events { get; set; } = new List<IssueEvent>();

    // Sub-issues hierarchy (1:N self-reference)
    public int? ParentIssueId { get; set; }
    public Issue? ParentIssue { get; set; }
    public ICollection<Issue> SubIssues { get; set; } = new List<Issue>();
}
