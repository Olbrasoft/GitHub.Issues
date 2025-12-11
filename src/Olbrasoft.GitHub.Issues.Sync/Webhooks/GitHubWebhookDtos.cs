using System.Text.Json.Serialization;

namespace Olbrasoft.GitHub.Issues.Sync.Webhooks;

/// <summary>
/// GitHub webhook payload for issues events.
/// </summary>
public record GitHubIssueWebhookPayload
{
    [JsonPropertyName("action")]
    public string Action { get; init; } = string.Empty;

    [JsonPropertyName("issue")]
    public GitHubWebhookIssue Issue { get; init; } = null!;

    [JsonPropertyName("repository")]
    public GitHubWebhookRepository Repository { get; init; } = null!;

    [JsonPropertyName("label")]
    public GitHubWebhookLabel? Label { get; init; }
}

/// <summary>
/// Issue data from webhook payload.
/// </summary>
public record GitHubWebhookIssue
{
    [JsonPropertyName("number")]
    public int Number { get; init; }

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("body")]
    public string? Body { get; init; }

    [JsonPropertyName("state")]
    public string State { get; init; } = string.Empty;

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; init; } = string.Empty;

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; init; }

    [JsonPropertyName("labels")]
    public List<GitHubWebhookLabel> Labels { get; init; } = new();

    [JsonPropertyName("pull_request")]
    public object? PullRequest { get; init; }

    [JsonPropertyName("comments")]
    public int Comments { get; init; }

    /// <summary>
    /// Returns true if this issue is actually a pull request.
    /// </summary>
    public bool IsPullRequest => PullRequest != null;
}

/// <summary>
/// Repository data from webhook payload.
/// </summary>
public record GitHubWebhookRepository
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("full_name")]
    public string FullName { get; init; } = string.Empty;

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; init; } = string.Empty;
}

/// <summary>
/// Label data from webhook payload.
/// </summary>
public record GitHubWebhookLabel
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("color")]
    public string Color { get; init; } = "ededed";
}

/// <summary>
/// Result of processing a webhook event.
/// </summary>
public record WebhookProcessingResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? IssueTitle { get; init; }
    public int? IssueNumber { get; init; }
    public string? RepositoryFullName { get; init; }
    public bool EmbeddingGenerated { get; init; }
}

// ===== Issue Comment Event =====

/// <summary>
/// GitHub webhook payload for issue_comment events.
/// </summary>
public record GitHubIssueCommentWebhookPayload
{
    [JsonPropertyName("action")]
    public string Action { get; init; } = string.Empty;

    [JsonPropertyName("issue")]
    public GitHubWebhookIssue Issue { get; init; } = null!;

    [JsonPropertyName("comment")]
    public GitHubWebhookComment Comment { get; init; } = null!;

    [JsonPropertyName("repository")]
    public GitHubWebhookRepository Repository { get; init; } = null!;
}

/// <summary>
/// Comment data from webhook payload.
/// </summary>
public record GitHubWebhookComment
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("body")]
    public string Body { get; init; } = string.Empty;

    [JsonPropertyName("user")]
    public GitHubWebhookUser? User { get; init; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// User data from webhook payload.
/// </summary>
public record GitHubWebhookUser
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("login")]
    public string Login { get; init; } = string.Empty;
}

// ===== Repository Event =====

/// <summary>
/// GitHub webhook payload for repository events.
/// </summary>
public record GitHubRepositoryWebhookPayload
{
    [JsonPropertyName("action")]
    public string Action { get; init; } = string.Empty;

    [JsonPropertyName("repository")]
    public GitHubWebhookRepository Repository { get; init; } = null!;
}

// ===== Label Event =====

/// <summary>
/// GitHub webhook payload for label events.
/// </summary>
public record GitHubLabelWebhookPayload
{
    [JsonPropertyName("action")]
    public string Action { get; init; } = string.Empty;

    [JsonPropertyName("label")]
    public GitHubWebhookLabel Label { get; init; } = null!;

    [JsonPropertyName("repository")]
    public GitHubWebhookRepository Repository { get; init; } = null!;

    /// <summary>
    /// For "edited" action, contains the changes made.
    /// </summary>
    [JsonPropertyName("changes")]
    public GitHubLabelChanges? Changes { get; init; }
}

/// <summary>
/// Changes to a label (for edited action).
/// </summary>
public record GitHubLabelChanges
{
    [JsonPropertyName("name")]
    public GitHubLabelNameChange? Name { get; init; }

    [JsonPropertyName("color")]
    public GitHubLabelColorChange? Color { get; init; }
}

/// <summary>
/// Label name change details.
/// </summary>
public record GitHubLabelNameChange
{
    [JsonPropertyName("from")]
    public string From { get; init; } = string.Empty;
}

/// <summary>
/// Label color change details.
/// </summary>
public record GitHubLabelColorChange
{
    [JsonPropertyName("from")]
    public string From { get; init; } = string.Empty;
}
