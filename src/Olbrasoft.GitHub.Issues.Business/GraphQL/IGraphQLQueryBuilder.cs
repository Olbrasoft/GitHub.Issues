using Olbrasoft.GitHub.Issues.Data.Dtos;

namespace Olbrasoft.GitHub.Issues.Business.GraphQL;

/// <summary>
/// Builds GraphQL queries for GitHub API.
/// Single responsibility: Query building only (SRP).
/// </summary>
public interface IGraphQLQueryBuilder
{
    /// <summary>
    /// Builds batch query for fetching issue bodies from multiple repositories.
    /// Groups issues by repository for efficient querying.
    /// </summary>
    /// <param name="issues">List of issues to fetch.</param>
    /// <returns>GraphQL query string.</returns>
    string BuildBatchIssueBodyQuery(IEnumerable<IssueBodyRequest> issues);
}
