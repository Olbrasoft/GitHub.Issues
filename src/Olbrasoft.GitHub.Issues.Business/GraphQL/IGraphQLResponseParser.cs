using System.Text.Json;
using Olbrasoft.GitHub.Issues.Data.Dtos;

namespace Olbrasoft.GitHub.Issues.Business.GraphQL;

/// <summary>
/// Parses GraphQL responses from GitHub API.
/// Single responsibility: Response parsing only (SRP).
/// </summary>
public interface IGraphQLResponseParser
{
    /// <summary>
    /// Parses batch issue body response from GraphQL data.
    /// Matches issue bodies with original requests.
    /// </summary>
    /// <param name="data">GraphQL response data element.</param>
    /// <param name="issues">Original issue requests (for matching).</param>
    /// <returns>Dictionary mapping (owner, repo, number) to body text.</returns>
    Dictionary<(string Owner, string Repo, int Number), string> ParseBatchIssueBodyResponse(
        JsonElement data,
        List<IssueBodyRequest> issues);
}
