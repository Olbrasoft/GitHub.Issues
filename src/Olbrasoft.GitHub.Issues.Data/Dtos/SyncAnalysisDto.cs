namespace Olbrasoft.GitHub.Issues.Data.Dtos;

/// <summary>
/// Analysis of what will be synced (without actually performing the sync).
/// Used to show user what will happen before making API calls to embedding providers.
/// </summary>
public class SyncAnalysisDto
{
    /// <summary>
    /// Repository name (owner/repo).
    /// </summary>
    public string RepositoryFullName { get; set; } = string.Empty;

    /// <summary>
    /// Total number of issues found on GitHub.
    /// </summary>
    public int TotalIssues { get; set; }

    /// <summary>
    /// Number of issues that don't exist in database yet.
    /// </summary>
    public int NewIssues { get; set; }

    /// <summary>
    /// Number of issues that exist but have been updated on GitHub.
    /// </summary>
    public int ChangedIssues { get; set; }

    /// <summary>
    /// Number of issues in database that are missing embeddings.
    /// </summary>
    public int MissingEmbeddings { get; set; }

    /// <summary>
    /// Number of Cohere API calls that will be made if user confirms embedding generation.
    /// Equals NewIssues + ChangedIssues (when not in full refresh mode).
    /// </summary>
    public int RequiredCohereApiCalls => NewIssues + ChangedIssues;
}
