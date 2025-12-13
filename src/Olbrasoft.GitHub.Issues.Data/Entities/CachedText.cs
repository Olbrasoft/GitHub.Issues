namespace Olbrasoft.GitHub.Issues.Data.Entities;

/// <summary>
/// Cache table for translated/generated text content (titles, summaries).
/// Uses composite primary key: (LanguageId, TextTypeId, IssueId).
/// </summary>
public class CachedText
{
    /// <summary>
    /// Foreign key to Language (LCID).
    /// Part of composite primary key.
    /// </summary>
    public int LanguageId { get; set; }

    /// <summary>
    /// Foreign key to TextType.
    /// Part of composite primary key.
    /// </summary>
    public int TextTypeId { get; set; }

    /// <summary>
    /// Foreign key to Issue.
    /// Part of composite primary key.
    /// </summary>
    public int IssueId { get; set; }

    /// <summary>
    /// The cached text content.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// When the cache entry was created.
    /// Used for cache invalidation - compared against Issue.GitHubUpdatedAt.
    /// </summary>
    public DateTime CachedAt { get; set; }

    // Navigation properties
    public Language Language { get; set; } = null!;
    public TextType TextType { get; set; } = null!;
    public Issue Issue { get; set; } = null!;
}
