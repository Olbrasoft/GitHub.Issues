namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Models;

public class SearchResultPage
{
    public List<IssueSearchResult> Results { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalPages { get; set; }

    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}
