namespace Olbrasoft.GitHub.Issues.Data.Dtos;

/// <summary>
/// Data Transfer Object for issue search results.
/// Contains only data - no presentation logic.
/// </summary>
public class IssueSearchResultDto
{
    /// <summary>Database ID of the issue.</summary>
    public int Id { get; set; }

    /// <summary>GitHub issue number.</summary>
    public int IssueNumber { get; set; }

    /// <summary>Issue title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Whether the issue is open.</summary>
    public bool IsOpen { get; set; }

    /// <summary>GitHub URL to the issue.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Repository full name (Owner/Repo).</summary>
    public string RepositoryFullName { get; set; } = string.Empty;

    /// <summary>Cosine similarity score (0.0-1.0).</summary>
    public double Similarity { get; set; }

    /// <summary>Issue labels with colors.</summary>
    public List<LabelDto> Labels { get; set; } = [];
}
