namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Services;

/// <summary>
/// Settings for GitHub API integration.
/// </summary>
public class GitHubSettings
{
    /// <summary>GitHub GraphQL API endpoint.</summary>
    public string GraphQLEndpoint { get; set; } = "https://api.github.com/graphql";

    /// <summary>GitHub personal access token. Store in User Secrets!</summary>
    public string Token { get; set; } = string.Empty;
}

/// <summary>
/// Settings for body preview display.
/// </summary>
public class BodyPreviewSettings
{
    /// <summary>Maximum characters to show in body preview.</summary>
    public int MaxLength { get; set; } = 200;
}
