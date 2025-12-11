using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Business;

/// <summary>
/// Business service interface for issue sync operations.
/// </summary>
public interface IIssueSyncBusinessService
{
    /// <summary>
    /// Gets an issue by repository ID and issue number.
    /// </summary>
    Task<Issue?> GetIssueAsync(int repositoryId, int number, CancellationToken ct = default);

    /// <summary>
    /// Gets all issues for a repository as a dictionary keyed by issue number.
    /// </summary>
    Task<Dictionary<int, Issue>> GetIssuesByRepositoryAsync(int repositoryId, CancellationToken ct = default);

    /// <summary>
    /// Saves (creates or updates) an issue.
    /// </summary>
    Task<Issue> SaveIssueAsync(
        int repositoryId,
        int number,
        string title,
        bool isOpen,
        string url,
        DateTimeOffset gitHubUpdatedAt,
        DateTimeOffset syncedAt,
        float[]? embedding = null,
        CancellationToken ct = default);

    /// <summary>
    /// Updates embedding for an existing issue.
    /// </summary>
    Task<bool> UpdateEmbeddingAsync(int issueId, float[] embedding, CancellationToken ct = default);

    /// <summary>
    /// Sets parent-child relationships for multiple issues in batch.
    /// </summary>
    Task<int> BatchSetParentsAsync(Dictionary<int, int?> childToParentMap, CancellationToken ct = default);

    /// <summary>
    /// Syncs labels for an issue.
    /// </summary>
    Task<bool> SyncLabelsAsync(int issueId, int repositoryId, List<string> labelNames, CancellationToken ct = default);

    /// <summary>
    /// Updates the comment count for an issue.
    /// </summary>
    Task<bool> UpdateCommentCountAsync(int repositoryId, int issueNumber, int commentCount, CancellationToken ct = default);
}
