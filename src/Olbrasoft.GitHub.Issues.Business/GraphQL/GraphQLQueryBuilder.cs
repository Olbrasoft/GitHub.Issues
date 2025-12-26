using System.Text;
using Olbrasoft.GitHub.Issues.Data.Dtos;

namespace Olbrasoft.GitHub.Issues.Business.GraphQL;

/// <summary>
/// Builds GraphQL queries for GitHub API.
/// Single responsibility: Query building only (SRP).
/// Extracted from GitHubGraphQLClient for better testability.
/// </summary>
public class GraphQLQueryBuilder : IGraphQLQueryBuilder
{
    public string BuildBatchIssueBodyQuery(IEnumerable<IssueBodyRequest> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);

        var issueList = issues.ToList();
        if (issueList.Count == 0)
        {
            return "{ }";
        }

        // Group issues by repository for efficient querying
        var byRepo = issueList.GroupBy(i => (i.Owner, i.Repo));

        var sb = new StringBuilder();
        sb.AppendLine("{");

        var repoIndex = 0;
        foreach (var repoGroup in byRepo)
        {
            var repoAlias = $"repo{repoIndex++}";
            sb.AppendLine($"  {repoAlias}: repository(owner: \"{repoGroup.Key.Owner}\", name: \"{repoGroup.Key.Repo}\") {{");

            foreach (var issue in repoGroup)
            {
                var issueAlias = $"issue{issue.Number}";
                sb.AppendLine($"    {issueAlias}: issue(number: {issue.Number}) {{ body number }}");
            }

            sb.AppendLine("  }");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }
}
