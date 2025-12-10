namespace Olbrasoft.GitHub.Issues.Data.Dtos;

/// <summary>
/// Data Transfer Object for repository search results.
/// </summary>
public class RepositorySearchResultDto
{
    /// <summary>Database ID of the repository.</summary>
    public int Id { get; set; }

    /// <summary>Full name of the repository (Owner/Repo).</summary>
    public string FullName { get; set; } = string.Empty;
}
