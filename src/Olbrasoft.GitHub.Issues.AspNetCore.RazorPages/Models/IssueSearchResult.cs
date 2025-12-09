using System.Globalization;

namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Models;

public class IssueSearchResult
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string HtmlUrl { get; set; } = string.Empty;
    public string RepositoryName { get; set; } = string.Empty;
    public double Similarity { get; set; }

    public string SimilarityPercent => (Similarity * 100).ToString("F1", CultureInfo.InvariantCulture) + "%";
    public bool IsOpen => State.Equals("open", StringComparison.OrdinalIgnoreCase);
    public string StateCzech => IsOpen ? "Otevřený" : "Zavřený";
}
