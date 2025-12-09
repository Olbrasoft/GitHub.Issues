using Pgvector;

namespace Olbrasoft.GitHub.Issues.Data.Entities;

public class Issue
{
    public int Id { get; set; }
    public int RepositoryId { get; set; }
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public string State { get; set; } = "open";
    public string HtmlUrl { get; set; } = string.Empty;
    public DateTimeOffset GitHubUpdatedAt { get; set; }
    public Vector? TitleEmbedding { get; set; }
    public string? SummaryCz { get; set; }
    public Vector? SummaryCzEmbedding { get; set; }
    public DateTimeOffset SyncedAt { get; set; }

    public Repository Repository { get; set; } = null!;
}
