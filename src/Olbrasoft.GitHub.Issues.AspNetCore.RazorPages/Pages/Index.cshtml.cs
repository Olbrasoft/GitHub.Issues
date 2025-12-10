using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Olbrasoft.GitHub.Issues.Business;
using Olbrasoft.GitHub.Issues.Business.Models;
using Olbrasoft.GitHub.Issues.Business.Services;

namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Pages;

public class IndexModel : PageModel
{
    private readonly IIssueSearchService _searchService;
    private readonly SearchSettings _searchSettings;

    public IndexModel(IIssueSearchService searchService, IOptions<SearchSettings> searchSettings)
    {
        _searchService = searchService;
        _searchSettings = searchSettings.Value;
    }

    [BindProperty(SupportsGet = true)]
    public string? Query { get; set; }

    [BindProperty(SupportsGet = true)]
    public string State { get; set; } = "all";

    [BindProperty(SupportsGet = true, Name = "PageNum")]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; }

    public SearchResultPage SearchResult { get; set; } = new();

    public int[] PageSizeOptions => _searchSettings.PageSizeOptions;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        // Set default page size from settings if not specified
        if (PageSize == 0)
        {
            PageSize = _searchSettings.DefaultPageSize;
        }

        // Ensure valid page number
        if (PageNumber < 1)
        {
            PageNumber = 1;
        }

        // Ensure valid page size
        if (!PageSizeOptions.Contains(PageSize))
        {
            PageSize = _searchSettings.DefaultPageSize;
        }

        if (!string.IsNullOrWhiteSpace(Query))
        {
            SearchResult = await _searchService.SearchAsync(Query, State, PageNumber, PageSize, cancellationToken);
        }
    }
}
