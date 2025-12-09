using Pgvector;

namespace Olbrasoft.GitHub.Issues.Data.Entities;

public class Issue
{
    public int Id { get; set; }
    public int RepositoryId { get; set; }
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsOpen { get; set; } = true;
    public string Url { get; set; } = string.Empty;
    public DateTimeOffset GitHubUpdatedAt { get; set; }
    public Vector TitleEmbedding { get; set; } = null!;
    public DateTimeOffset SyncedAt { get; set; }

    public Repository Repository { get; set; } = null!;
    public ICollection<IssueLabel> IssueLabels { get; set; } = new List<IssueLabel>();
    public ICollection<IssueEvent> Events { get; set; } = new List<IssueEvent>();

    // Sub-issues hierarchy (1:N self-reference)
    public int? ParentIssueId { get; set; }
    public Issue? ParentIssue { get; set; }
    public ICollection<Issue> SubIssues { get; set; } = new List<Issue>();
}
