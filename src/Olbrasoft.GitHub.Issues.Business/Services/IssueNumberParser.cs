using System.Text.RegularExpressions;

namespace Olbrasoft.GitHub.Issues.Business.Services;

/// <summary>
/// Parses issue number patterns from search queries.
/// Supports: #123, 123, issue 123, issues #123, repo#123, owner/repo#123
/// </summary>
public static partial class IssueNumberParser
{
    // Patterns to match issue numbers
    // Group "repo": optional repository reference (owner/repo or just repo)
    // Group "num": the issue number
    [GeneratedRegex(@"(?:(?<repo>[\w\-\.]+(?:/[\w\-\.]+)?)\s*)?#(?<num>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex RepoHashNumberPattern();

    [GeneratedRegex(@"(?:issues?\s+)?#?(?<num>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex SimpleNumberPattern();

    /// <summary>
    /// Parsed result containing extracted issue number and optional repository reference.
    /// </summary>
    public record ParsedIssueNumber(int Number, string? RepositoryName);

    /// <summary>
    /// Attempts to parse issue number(s) from the query string.
    /// </summary>
    /// <param name="query">Search query string</param>
    /// <returns>List of parsed issue numbers with optional repository references</returns>
    public static List<ParsedIssueNumber> Parse(string? query)
    {
        var results = new List<ParsedIssueNumber>();

        if (string.IsNullOrWhiteSpace(query))
            return results;

        var trimmed = query.Trim();

        // First try repo#number pattern (e.g., VirtualAssistant#123 or Olbrasoft/VirtualAssistant#123)
        var repoMatch = RepoHashNumberPattern().Match(trimmed);
        if (repoMatch.Success)
        {
            var num = int.Parse(repoMatch.Groups["num"].Value);
            var repo = repoMatch.Groups["repo"].Success ? repoMatch.Groups["repo"].Value : null;

            // Exclude "issue" and "issues" as repo names - they are keywords
            if (repo == null || !repo.Equals("issue", StringComparison.OrdinalIgnoreCase) &&
                !repo.Equals("issues", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(new ParsedIssueNumber(num, repo));
                return results;
            }
        }

        // Try simple patterns: #123, 123, issue 123, issues #123
        var simpleMatch = SimpleNumberPattern().Match(trimmed);
        if (simpleMatch.Success)
        {
            // Only match if it's the primary part of the query (not buried in text)
            // Check if the number is at start or after "issue(s)" keyword
            var num = int.Parse(simpleMatch.Groups["num"].Value);

            // Validate: query should be mostly about the number
            // e.g., "#27", "27", "issue 27", "issues #27" - yes
            // e.g., "fix bug 27 times" - no (27 is not an issue number here)
            if (IsLikelyIssueNumber(trimmed, simpleMatch))
            {
                results.Add(new ParsedIssueNumber(num, null));
            }
        }

        return results;
    }

    /// <summary>
    /// Checks if the matched number is likely an issue number reference.
    /// </summary>
    private static bool IsLikelyIssueNumber(string query, Match match)
    {
        // If query starts with # followed by number, it's definitely an issue number
        if (query.StartsWith('#'))
            return true;

        // If query is just a number
        if (int.TryParse(query, out _))
            return true;

        // If query contains "issue" or "issues" before the number
        var beforeMatch = query.Substring(0, match.Index).ToLowerInvariant();
        if (beforeMatch.Contains("issue"))
            return true;

        // If the entire query is short and contains the match at the start
        if (query.Length < 20 && match.Index < 10)
            return true;

        return false;
    }

    /// <summary>
    /// Extracts the semantic search portion of the query (excluding issue number patterns).
    /// Returns null if the entire query is just an issue number.
    /// </summary>
    public static string? GetSemanticQuery(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return null;

        var trimmed = query.Trim();

        // Remove repo#number patterns
        var withoutRepoHash = RepoHashNumberPattern().Replace(trimmed, "");

        // Remove simple issue patterns only if they're clearly issue references
        // Be more conservative here to avoid removing numbers from semantic queries
        if (trimmed.StartsWith('#') || trimmed.ToLowerInvariant().StartsWith("issue"))
        {
            withoutRepoHash = SimpleNumberPattern().Replace(withoutRepoHash, "");
        }

        var result = withoutRepoHash.Trim();
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }
}
