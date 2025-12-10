using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.GitHub.Issues.Business;
using Olbrasoft.GitHub.Issues.Business.Models;
using Olbrasoft.GitHub.Issues.Business.Services;
using Olbrasoft.GitHub.Issues.Data.Dtos;
using Olbrasoft.GitHub.Issues.Data.Queries.RepositoryQueries;
using Olbrasoft.Mediation;

namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Pages;

public class IndexModel : PageModel
{
    private readonly IIssueSearchService _searchService;
    private readonly SearchSettings _searchSettings;
    private readonly IMediator _mediator;

    public IndexModel(IIssueSearchService searchService, IOptions<SearchSettings> searchSettings, IMediator mediator)
    {
        _searchService = searchService;
        _searchSettings = searchSettings.Value;
        _mediator = mediator;
    }

    [BindProperty(SupportsGet = true)]
    public string? Query { get; set; }

    [BindProperty(SupportsGet = true)]
    public string State { get; set; } = "all";

    [BindProperty(SupportsGet = true, Name = "PageNum")]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Repos { get; set; }

    public SearchResultPage SearchResult { get; set; } = new();

    public int[] PageSizeOptions => _searchSettings.PageSizeOptions;

    public IReadOnlyList<int> SelectedRepositoryIds => ParseRepositoryIds(Repos);

    public IEnumerable<RepositorySearchResultDto> SelectedRepositories { get; set; } = Enumerable.Empty<RepositorySearchResultDto>();

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

        // Load selected repository info
        var repositoryIds = SelectedRepositoryIds;
        if (repositoryIds.Count > 0)
        {
            var repoQuery = new RepositoriesByIdsQuery(_mediator) { Ids = repositoryIds };
            SelectedRepositories = await repoQuery.ToResultAsync(cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(Query))
        {
            var repoIdsForSearch = repositoryIds.Count > 0 ? repositoryIds : null;
            SearchResult = await _searchService.SearchAsync(Query, State, PageNumber, PageSize, repoIdsForSearch, cancellationToken);
        }
    }

    private static IReadOnlyList<int> ParseRepositoryIds(string? repos)
    {
        if (string.IsNullOrWhiteSpace(repos))
        {
            return Array.Empty<int>();
        }

        return repos
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s.Trim(), out var id) ? id : 0)
            .Where(id => id > 0)
            .ToList();
    }
}
