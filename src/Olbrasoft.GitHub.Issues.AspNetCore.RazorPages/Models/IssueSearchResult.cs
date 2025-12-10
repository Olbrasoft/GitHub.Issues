using System.Globalization;

namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Models;

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

    /// <summary>Issue title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Whether the issue is currently open.</summary>
    public bool IsOpen { get; set; }

    /// <summary>GitHub URL to the issue.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Full name of the repository (Owner/Repo).</summary>
    public string RepositoryName { get; set; } = string.Empty;

    /// <summary>Cosine similarity score (0.0-1.0) from vector search.</summary>
    public double Similarity { get; set; }

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
}
