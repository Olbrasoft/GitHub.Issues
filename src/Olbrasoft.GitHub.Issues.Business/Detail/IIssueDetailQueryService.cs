using Olbrasoft.GitHub.Issues.Data.Dtos;
using Olbrasoft.GitHub.Issues.Data.Entities;

namespace Olbrasoft.GitHub.Issues.Business.Detail;

/// <summary>
/// Service for querying issue details from the database.
/// Single responsibility: Database queries only (SRP).
/// </summary>
public interface IIssueDetailQueryService
{
    /// <summary>
    /// Gets detailed information about a specific issue from the database.
    /// </summary>
    /// <param name="issueId">The internal issue ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Issue detail result or null if not found.</returns>
    Task<IssueDetailResult> GetIssueDetailAsync(int issueId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets multiple issues by their IDs with repository information.
    /// </summary>
    /// <param name="issueIds">The issue IDs to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of issues with repository data.</returns>
    Task<List<Issue>> GetIssuesByIdsAsync(IEnumerable<int> issueIds, CancellationToken cancellationToken = default);
}
