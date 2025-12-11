using System.Globalization;
using Olbrasoft.GitHub.Issues.Data.Dtos;

namespace Olbrasoft.GitHub.Issues.Business.Models;

/// <summary>
/// Individual search result representing a GitHub issue.
/// </summary>
/// <remarks>
/// ARCHITECTURAL NOTE: This model includes computed presentation properties (SimilarityPercent, StateCzech).
/// This is intentional - the properties are simple, pure formatting helpers in the presentation layer.
/// See issue #58 for trade-off discussion.
/// </remarks>
public class IssueSearchResult
{
    /// <summary>Database ID of the issue.</summary>
    public int Id { get; set; }

    /// <summary>GitHub issue number (not database ID).</summary>
    public int IssueNumber { get; set; }

    /// <summary>Issue title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Whether the issue is currently open.</summary>
    public bool IsOpen { get; set; }

    /// <summary>GitHub URL to the issue.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Repository owner (e.g., "Olbrasoft").</summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>Repository name (e.g., "GitHub.Issues").</summary>
    public string RepoName { get; set; } = string.Empty;

    /// <summary>Full name of the repository (Owner/Repo).</summary>
    public string RepositoryName { get; set; } = string.Empty;

    /// <summary>Cosine similarity score (0.0-1.0) from vector search.</summary>
    public double Similarity { get; set; }

    /// <summary>Whether this result is from exact issue number match (vs semantic search).</summary>
    public bool IsExactMatch { get; set; }

    /// <summary>Full issue body content (fetched from GitHub GraphQL API).</summary>
    public string? Body { get; set; }

    /// <summary>Issue labels with colors.</summary>
    public List<LabelDto> Labels { get; set; } = [];

    /// <summary>
    /// Maximum length for body preview. Set from AiSummarySettings.MaxLength.
    /// </summary>
    public int PreviewMaxLength { get; set; } = 500;

    /// <summary>
    /// Short preview of issue body for list display.
    /// Computed property that truncates at sentence boundary.
    /// Uses PreviewMaxLength from configuration.
    /// </summary>
    public string BodyPreview => GetPreview(Body, PreviewMaxLength);

    /// <summary>
    /// Similarity formatted as percentage for display (e.g., "85.3%").
    /// Computed property for UI convenience.
    /// </summary>
    public string SimilarityPercent => (Similarity * 100).ToString("F1", CultureInfo.InvariantCulture) + "%";

    /// <summary>
    /// Issue state localized to Czech ("Otevřený"/"Zavřený").
    /// Computed property for Czech language UI.
    /// </summary>
    public string StateCzech => IsOpen ? "Otevřený" : "Zavřený";

    /// <summary>
    /// Generates a preview of the body text, truncating at sentence boundary if possible.
    /// </summary>
    private static string GetPreview(string? body, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(body))
            return string.Empty;

        // Remove markdown headers and normalize whitespace
        var text = body.Replace("#", "").Trim();
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");

        if (text.Length <= maxLength)
            return text;

        // Find last sentence boundary before maxLength
        var truncated = text.Substring(0, maxLength);
        var lastPeriod = truncated.LastIndexOf('.');

        if (lastPeriod > maxLength / 2)
            return truncated.Substring(0, lastPeriod + 1);

        return truncated + "...";
    }
}
