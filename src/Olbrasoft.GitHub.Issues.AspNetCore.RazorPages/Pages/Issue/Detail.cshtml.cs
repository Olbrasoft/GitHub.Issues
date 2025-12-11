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
    private const string SessionKeyTranslateToCzech = "Search.TranslateToCzech";
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
    /// Language preference for summaries: "both" (default, when TranslateToCzech=true) or "en" (when false).
    /// Read from session, set by Index page checkbox.
    /// </summary>
    public string SummaryLanguage { get; private set; } = "both";

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        // Read TranslateToCzech preference from session (default: true = "both")
        var translateToCzechValue = HttpContext.Session.GetInt32(SessionKeyTranslateToCzech);
        var translateToCzech = translateToCzechValue == null || translateToCzechValue.Value == 1;
        SummaryLanguage = translateToCzech ? "both" : "en";

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
}
