using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Models;
using Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Services;

namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Pages;

public class IndexModel : PageModel
{
    private readonly IssueSearchService _searchService;

    public IndexModel(IssueSearchService searchService)
    {
        _searchService = searchService;
    }

    [BindProperty(SupportsGet = true)]
    public string? Query { get; set; }

    [BindProperty(SupportsGet = true)]
    public string State { get; set; } = "all";

    [BindProperty(SupportsGet = true, Name = "Page")]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 10;

    public SearchResultPage SearchResult { get; set; } = new();

    public static readonly int[] PageSizeOptions = { 10, 25, 50 };

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        // Ensure valid page number
        if (PageNumber < 1)
        {
            PageNumber = 1;
        }

        // Ensure valid page size
        if (!PageSizeOptions.Contains(PageSize))
        {
            PageSize = 10;
        }

        if (!string.IsNullOrWhiteSpace(Query))
        {
            SearchResult = await _searchService.SearchAsync(Query, State, PageNumber, PageSize, cancellationToken);
        }
    }
}
