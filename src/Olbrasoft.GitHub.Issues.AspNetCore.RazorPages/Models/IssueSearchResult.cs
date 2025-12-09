using System.Globalization;

namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Models;

public class IssueSearchResult
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsOpen { get; set; }
    public string Url { get; set; } = string.Empty;
    public string RepositoryName { get; set; } = string.Empty;
    public double Similarity { get; set; }

    public string SimilarityPercent => (Similarity * 100).ToString("F1", CultureInfo.InvariantCulture) + "%";
    public string StateCzech => IsOpen ? "Otevřený" : "Zavřený";
}
