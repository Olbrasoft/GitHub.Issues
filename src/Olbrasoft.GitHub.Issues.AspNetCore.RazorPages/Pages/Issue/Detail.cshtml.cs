using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Olbrasoft.GitHub.Issues.Business;
using Olbrasoft.GitHub.Issues.Business.Services;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;

namespace Olbrasoft.GitHub.Issues.AspNetCore.RazorPages.Pages.Issue;

public class DetailModel : PageModel
{
    private readonly GitHubDbContext _dbContext;
    private readonly IGitHubGraphQLClient _graphQLClient;
    private readonly IAiSummarizationService _summarizationService;
    private readonly ILogger<DetailModel> _logger;

    public DetailModel(
        GitHubDbContext dbContext,
        IGitHubGraphQLClient graphQLClient,
        IAiSummarizationService summarizationService,
        ILogger<DetailModel> logger)
    {
        _dbContext = dbContext;
        _graphQLClient = graphQLClient;
        _summarizationService = summarizationService;
        _logger = logger;
    }

    public IssueDetail? Issue { get; set; }

    public string? ErrorMessage { get; set; }

    public string? Summary { get; set; }

    public string? SummaryError { get; set; }

    public string? SummaryProvider { get; set; }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        var issue = await _dbContext.Issues
            .Include(i => i.Repository)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (issue == null)
        {
            ErrorMessage = "Issue nenalezeno.";
            return Page();
        }

        var parts = issue.Repository.FullName.Split('/');
        var owner = parts.Length == 2 ? parts[0] : string.Empty;
        var repoName = parts.Length == 2 ? parts[1] : string.Empty;

        Issue = new IssueDetail
        {
            Id = issue.Id,
            IssueNumber = issue.Number,
            Title = issue.Title,
            IsOpen = issue.IsOpen,
            Url = issue.Url,
            Owner = owner,
            RepoName = repoName,
            RepositoryName = issue.Repository.FullName
        };

        // Fetch body from GitHub GraphQL API
        if (!string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(repoName))
        {
            var requests = new[] { new IssueBodyRequest(owner, repoName, issue.Number) };
            var bodies = await _graphQLClient.FetchBodiesAsync(requests, cancellationToken);

            var key = (owner, repoName, issue.Number);
            if (bodies.TryGetValue(key, out var body))
            {
                Issue.Body = body;

                // Generate AI summary
                if (!string.IsNullOrWhiteSpace(body))
                {
                    var result = await _summarizationService.SummarizeAsync(body, cancellationToken);
                    if (result.Success)
                    {
                        Summary = result.Summary;
                        SummaryProvider = $"{result.Provider}/{result.Model}";
                    }
                    else
                    {
                        SummaryError = result.Error;
                        _logger.LogWarning("Failed to summarize issue {Id}: {Error}", id, result.Error);
                    }
                }
            }
        }

        return Page();
    }
}

/// <summary>
/// Issue detail model for the detail page.
/// </summary>
public class IssueDetail
{
    public int Id { get; set; }
    public int IssueNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsOpen { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string RepoName { get; set; } = string.Empty;
    public string RepositoryName { get; set; } = string.Empty;
    public string? Body { get; set; }

    public string StateCzech => IsOpen ? "Otevřený" : "Zavřený";
}
