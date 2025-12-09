namespace Olbrasoft.GitHub.Issues.Sync.Services;

public class GitHubSettings
{
    public string? Token { get; set; }
    public List<string> Repositories { get; set; } = new();
}
