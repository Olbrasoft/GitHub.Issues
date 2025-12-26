using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Data.Repositories;

/// <summary>
/// Repository abstraction for Issue entity operations.
/// Follows Dependency Inversion Principle - Business layer depends on this abstraction,
/// not on concrete EF Core implementation.
/// </summary>
public interface IIssueRepository
{
    /// <summary>
    /// Gets an issue with full details including repository and labels.
    /// Used for displaying issue detail page.
    /// </summary>
    /// <param name="issueId">Internal issue ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Issue with Repository and IssueLabels.Label includes, or null if not found</returns>
    Task<Issue?> GetIssueWithDetailsAsync(int issueId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an issue with repository information only.
    /// Used for operations that need repository context but not labels.
    /// </summary>
    /// <param name="issueId">Internal issue ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Issue with Repository include, or null if not found</returns>
    Task<Issue?> GetIssueWithRepositoryAsync(int issueId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets multiple issues by their IDs with repository information.
    /// Used for batch operations like fetching bodies for multiple issues.
    /// </summary>
    /// <param name="issueIds">List of internal issue IDs</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of issues with Repository includes</returns>
    Task<List<Issue>> GetIssuesByIdsWithRepositoryAsync(List<int> issueIds, CancellationToken cancellationToken = default);
}
