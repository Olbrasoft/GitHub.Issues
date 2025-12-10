namespace Olbrasoft.GitHub.Issues.Data.Entities;

public class Repository
{
    public int Id { get; set; }
    public long GitHubId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string HtmlUrl { get; set; } = string.Empty;
    public DateTimeOffset? LastSyncedAt { get; set; }

    public ICollection<Issue> Issues { get; set; } = new List<Issue>();
    public ICollection<Label> Labels { get; set; } = new List<Label>();
}
