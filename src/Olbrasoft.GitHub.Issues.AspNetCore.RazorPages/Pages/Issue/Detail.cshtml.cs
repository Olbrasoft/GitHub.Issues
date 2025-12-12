using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Olbrasoft.GitHub.Issues.Business;

namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Pages.Issue;

/// <summary>
/// Page model for issue detail page.
/// Single responsibility: Handle HTTP requests and map to view properties.
/// </summary>
public class DetailModel : PageModel
{
    private const string SessionKeyLanguage = "Search.Language";
    private readonly IIssueDetailService _issueDetailService;

    public DetailModel(IIssueDetailService issueDetailService)
    {
        _issueDetailService = issueDetailService;
    }

    public IssueDetailDto? Issue { get; set; }

    public string? ErrorMessage { get; set; }

    public string? Summary { get; set; }

    public string? SummaryError { get; set; }

    public string? SummaryProvider { get; set; }

    /// <summary>
    /// Indicates whether summary is being generated asynchronously.
    /// When true, the view should show a loading indicator and connect to SignalR.
    /// </summary>
    public bool SummaryPending { get; set; }

    /// <summary>
    /// URL to return to after viewing the detail (e.g., search results page).
    /// Falls back to homepage if not provided.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    /// <summary>
    /// Gets the safe return URL - defaults to homepage if not provided.
    /// </summary>
    public string SafeReturnUrl => string.IsNullOrWhiteSpace(ReturnUrl) ? "/" : ReturnUrl;

    /// <summary>
    /// Selected language for translations (en, de, cs).
    /// Read from session, shared with Index page language selector.
    /// </summary>
    public string Language { get; private set; } = "cs";

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        // Read language preference from session (default: "cs" - Czech)
        var savedLanguage = HttpContext.Session.GetString(SessionKeyLanguage);
        if (!string.IsNullOrEmpty(savedLanguage) && IsValidLanguage(savedLanguage))
            Language = savedLanguage;

        var result = await _issueDetailService.GetIssueDetailAsync(id, cancellationToken);

        if (!result.Found)
        {
            ErrorMessage = result.ErrorMessage;
            return Page();
        }

        Issue = result.Issue;
        Summary = result.Summary;
        SummaryProvider = result.SummaryProvider;
        SummaryError = result.SummaryError;
        SummaryPending = result.SummaryPending;

        return Page();
    }

    private static bool IsValidLanguage(string? language)
        => language is "en" or "de" or "cs";
}
