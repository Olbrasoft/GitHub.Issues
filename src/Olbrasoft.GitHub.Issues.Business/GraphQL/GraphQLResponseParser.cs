using System.Text.Json;
using Microsoft.Extensions.Logging;
using Olbrasoft.GitHub.Issues.Data.Dtos;

namespace Olbrasoft.GitHub.Issues.Business.GraphQL;

/// <summary>
/// Parses GraphQL responses from GitHub API.
/// Single responsibility: Response parsing only (SRP).
/// Extracted from GitHubGraphQLClient for better testability.
/// </summary>
public class GraphQLResponseParser : IGraphQLResponseParser
{
    private readonly ILogger<GraphQLResponseParser> _logger;

    public GraphQLResponseParser(ILogger<GraphQLResponseParser> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
    }

    public Dictionary<(string Owner, string Repo, int Number), string> ParseBatchIssueBodyResponse(
        JsonElement data,
        List<IssueBodyRequest> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);

        var result = new Dictionary<(string Owner, string Repo, int Number), string>();

        // Group issues by repository to match query structure
        var byRepo = issues.GroupBy(i => (i.Owner, i.Repo)).ToList();

        var repoIndex = 0;
        foreach (var repoGroup in byRepo)
        {
            var repoAlias = $"repo{repoIndex++}";

            if (!data.TryGetProperty(repoAlias, out var repoData) || repoData.ValueKind == JsonValueKind.Null)
            {
                _logger.LogWarning("Repository {Owner}/{Repo} not found in GraphQL response",
                    repoGroup.Key.Owner, repoGroup.Key.Repo);
                continue;
            }

            foreach (var issue in repoGroup)
            {
                var issueAlias = $"issue{issue.Number}";

                if (repoData.TryGetProperty(issueAlias, out var issueData) &&
                    issueData.ValueKind != JsonValueKind.Null &&
                    issueData.TryGetProperty("body", out var bodyProp))
                {
                    var body = bodyProp.GetString() ?? string.Empty;
                    result[(repoGroup.Key.Owner, repoGroup.Key.Repo, issue.Number)] = body;
                }
                else
                {
                    _logger.LogDebug("Issue {Owner}/{Repo}#{Number} body not found in response",
                        issue.Owner, issue.Repo, issue.Number);
                }
            }
        }

        return result;
    }
}
