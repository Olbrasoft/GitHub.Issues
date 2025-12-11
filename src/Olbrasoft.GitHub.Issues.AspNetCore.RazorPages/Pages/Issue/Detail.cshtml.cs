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

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
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

        return Page();
    }
}
