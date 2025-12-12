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
    private readonly AiSummarySettings _aiSummarySettings;
    private readonly IMediator _mediator;

    // Session keys for search state persistence
    private const string SessionKeyQuery = "Search.Query";
    private const string SessionKeyRepos = "Search.Repos";
    private const string SessionKeyState = "Search.State";
    private const string SessionKeyPageSize = "Search.PageSize";
    private const string SessionKeyLanguage = "Search.Language";

    public IndexModel(IIssueSearchService searchService, IOptions<SearchSettings> searchSettings, IOptions<AiSummarySettings> aiSummarySettings, IMediator mediator)
    {
        _searchService = searchService;
        _searchSettings = searchSettings.Value;
        _aiSummarySettings = aiSummarySettings.Value;
        _mediator = mediator;
    }

    [BindProperty(SupportsGet = true)]
    public string? Query { get; set; }

    [BindProperty(SupportsGet = true)]
    public string State { get; set; } = "open";

    [BindProperty(SupportsGet = true, Name = "PageNum")]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Repos { get; set; }

    /// <summary>
    /// Target language for translations (en, de, cs).
    /// Default: cs (Czech user preference).
    /// </summary>
    [BindProperty(SupportsGet = true, Name = "Lang")]
    public string Language { get; set; } = "cs";

    public SearchResultPage SearchResult { get; set; } = new();

    /// <summary>
    /// Indicates whether a search was performed (Query has value OR repos are selected).
    /// </summary>
    public bool HasSearchCriteria { get; set; }

    private static readonly int[] DefaultPageSizeOptions = [10, 25, 50];
    public int[] PageSizeOptions => _searchSettings.PageSizeOptions.Length > 0
        ? _searchSettings.PageSizeOptions
        : DefaultPageSizeOptions;

    public IReadOnlyList<int> SelectedRepositoryIds => ParseRepositoryIds(Repos);

    public IEnumerable<RepositorySearchResultDto> SelectedRepositories { get; set; } = Enumerable.Empty<RepositorySearchResultDto>();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        // Check if URL has any search parameters
        var hasUrlParams = HasUrlSearchParameters();

        if (hasUrlParams)
        {
            // URL has parameters → use them and save to session
            SaveSearchStateToSession();
        }
        else
        {
            // URL is empty → try to load from session
            LoadSearchStateFromSession();
        }

        // Set default page size from settings if not specified
        if (PageSize == 0)
        {
            PageSize = _searchSettings.DefaultPageSize;
        }

        // Validate Language (must be one of: en, de, cs)
        if (!IsValidLanguage(Language))
        {
            Language = "cs"; // Default to Czech
        }

        // Ensure valid page number (always start at page 1 for new searches)
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

        // Search criteria: has query OR has selected repositories
        var hasQuery = !string.IsNullOrWhiteSpace(Query);
        var hasRepos = repositoryIds.Count > 0;
        HasSearchCriteria = hasQuery || hasRepos;

        if (HasSearchCriteria)
        {
            var repoIdsForSearch = hasRepos ? repositoryIds : null;
            // Use empty string as query if no query provided (will search all in selected repos)
            var searchQuery = hasQuery ? Query! : "";
            SearchResult = await _searchService.SearchAsync(searchQuery, State, PageNumber, PageSize, repoIdsForSearch, cancellationToken);
        }
    }

    /// <summary>
    /// Clears the saved search state from session.
    /// </summary>
    public IActionResult OnPostClearSearch()
    {
        HttpContext.Session.Remove(SessionKeyQuery);
        HttpContext.Session.Remove(SessionKeyRepos);
        HttpContext.Session.Remove(SessionKeyState);
        HttpContext.Session.Remove(SessionKeyPageSize);
        HttpContext.Session.Remove(SessionKeyLanguage);

        return RedirectToPage();
    }

    private bool HasUrlSearchParameters()
    {
        var request = HttpContext?.Request;
        if (request == null) return false;

        return request.Query.ContainsKey("Query") ||
               request.Query.ContainsKey("Repos") ||
               request.Query.ContainsKey("State") ||
               request.Query.ContainsKey("PageSize") ||
               request.Query.ContainsKey("Lang");
    }

    private void SaveSearchStateToSession()
    {
        var session = HttpContext?.Session;
        if (session == null) return;

        if (!string.IsNullOrEmpty(Query))
            session.SetString(SessionKeyQuery, Query);
        else
            session.Remove(SessionKeyQuery);

        if (!string.IsNullOrEmpty(Repos))
            session.SetString(SessionKeyRepos, Repos);
        else
            session.Remove(SessionKeyRepos);

        if (!string.IsNullOrEmpty(State))
            session.SetString(SessionKeyState, State);

        if (PageSize > 0)
            session.SetInt32(SessionKeyPageSize, PageSize);

        // Store Language preference
        if (!string.IsNullOrEmpty(Language))
            session.SetString(SessionKeyLanguage, Language);
    }

    private void LoadSearchStateFromSession()
    {
        var session = HttpContext?.Session;
        if (session == null) return;

        var savedQuery = session.GetString(SessionKeyQuery);
        if (!string.IsNullOrEmpty(savedQuery))
            Query = savedQuery;

        var savedRepos = session.GetString(SessionKeyRepos);
        if (!string.IsNullOrEmpty(savedRepos))
            Repos = savedRepos;

        var savedState = session.GetString(SessionKeyState);
        if (!string.IsNullOrEmpty(savedState))
            State = savedState;

        var savedPageSize = session.GetInt32(SessionKeyPageSize);
        if (savedPageSize.HasValue && savedPageSize.Value > 0)
            PageSize = savedPageSize.Value;

        // Load Language preference (default: "cs" if not in session)
        var savedLanguage = session.GetString(SessionKeyLanguage);
        if (!string.IsNullOrEmpty(savedLanguage) && IsValidLanguage(savedLanguage))
            Language = savedLanguage;
    }

    private static bool IsValidLanguage(string? language)
    {
        return language is "en" or "de" or "cs";
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
