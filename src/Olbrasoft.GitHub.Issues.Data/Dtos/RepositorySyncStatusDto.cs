namespace Olbrasoft.GitHub.Issues.Data.Dtos;

/// <summary>
/// Data Transfer Object for repository sync status.
/// Contains sync information for the intelligent sync UI.
/// </summary>
public class RepositorySyncStatusDto
{
    /// <summary>Database ID of the repository.</summary>
    public int Id { get; set; }

    /// <summary>Full name of the repository (Owner/Repo).</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Number of issues in this repository.</summary>
    public int IssueCount { get; set; }

    /// <summary>When the repository was last synchronized.</summary>
    public DateTimeOffset? LastSyncedAt { get; set; }
}
