namespace Olbrasoft.GitHub.Issues.Sync.Services;

public class GitHubSettings
{
    public string? Token { get; set; }

    /// <summary>
    /// Explicit list of repositories to sync (e.g., ["Olbrasoft/VirtualAssistant", "Olbrasoft/Blog"]).
    /// If empty, Owner-based discovery is used.
    /// </summary>
    public List<string> Repositories { get; set; } = new();

    /// <summary>
    /// Owner (user or organization) for dynamic repository discovery (e.g., "Olbrasoft").
    /// Used only when Repositories list is empty.
    /// </summary>
    public string? Owner { get; set; }

    /// <summary>
    /// Type of owner: "user" or "org". Default is "user".
    /// </summary>
    public string OwnerType { get; set; } = "user";

    /// <summary>
    /// Include archived repositories in dynamic discovery. Default is false.
    /// </summary>
    public bool IncludeArchived { get; set; } = false;

    /// <summary>
    /// Include forked repositories in dynamic discovery. Default is false.
    /// </summary>
    public bool IncludeForks { get; set; } = false;
}
