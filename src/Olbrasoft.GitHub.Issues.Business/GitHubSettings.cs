namespace Olbrasoft.GitHub.Issues.Business;

/// <summary>
/// Settings for GitHub API integration.
/// </summary>
public class GitHubSettings
{
    /// <summary>GitHub GraphQL API endpoint.</summary>
    public string GraphQLEndpoint { get; set; } = "https://api.github.com/graphql";

    /// <summary>GitHub personal access token. Store in User Secrets!</summary>
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

/// <summary>
/// Settings for body preview display.
/// </summary>
public class BodyPreviewSettings
{
    /// <summary>Maximum characters to show in body preview.</summary>
    public int MaxLength { get; set; } = 200;
}
