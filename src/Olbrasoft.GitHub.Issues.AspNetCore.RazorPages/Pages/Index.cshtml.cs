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

    public List<IssueSearchResult> Results { get; set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(Query))
        {
            Results = await _searchService.SearchAsync(Query, State, cancellationToken: cancellationToken);
        }
    }
}
