using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;

namespace Olbrasoft.GitHub.Issues.Business.Services;

/// <summary>
/// Service for fetching issue details including body from GraphQL and AI summary.
/// Single responsibility: Orchestrate data retrieval for issue detail page.
/// </summary>
public class IssueDetailService : IIssueDetailService
{
    private readonly GitHubDbContext _dbContext;
    private readonly IGitHubGraphQLClient _graphQLClient;
    private readonly IAiSummarizationService _summarizationService;
    private readonly ILogger<IssueDetailService> _logger;

    public IssueDetailService(
        GitHubDbContext dbContext,
        IGitHubGraphQLClient graphQLClient,
        IAiSummarizationService summarizationService,
        ILogger<IssueDetailService> logger)
    {
        _dbContext = dbContext;
        _graphQLClient = graphQLClient;
        _summarizationService = summarizationService;
        _logger = logger;
    }

    public async Task<IssueDetailResult> GetIssueDetailAsync(int issueId, CancellationToken cancellationToken = default)
    {
        var issue = await _dbContext.Issues
            .Include(i => i.Repository)
            .FirstOrDefaultAsync(i => i.Id == issueId, cancellationToken);

        if (issue == null)
        {
            return new IssueDetailResult(
                Found: false,
                Issue: null,
                Summary: null,
                SummaryProvider: null,
                SummaryError: null,
                ErrorMessage: "Issue nenalezeno.");
        }

        var (owner, repoName) = ParseRepositoryFullName(issue.Repository.FullName);

        var issueDto = new IssueDetailDto(
            Id: issue.Id,
            IssueNumber: issue.Number,
            Title: issue.Title,
            IsOpen: issue.IsOpen,
            Url: issue.Url,
            Owner: owner,
            RepoName: repoName,
            RepositoryName: issue.Repository.FullName,
            Body: null);

        // Fetch body from GitHub GraphQL API
        string? body = null;
        if (!string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(repoName))
        {
            var requests = new[] { new IssueBodyRequest(owner, repoName, issue.Number) };
            var bodies = await _graphQLClient.FetchBodiesAsync(requests, cancellationToken);

            var key = (owner, repoName, issue.Number);
            if (bodies.TryGetValue(key, out body))
            {
                issueDto = issueDto with { Body = body };
            }
        }

        // Generate AI summary
        string? summary = null;
        string? summaryProvider = null;
        string? summaryError = null;

        if (!string.IsNullOrWhiteSpace(body))
        {
            var result = await _summarizationService.SummarizeAsync(body, cancellationToken);
            if (result.Success)
            {
                summary = result.Summary;
                summaryProvider = $"{result.Provider}/{result.Model}";
            }
            else
            {
                summaryError = result.Error;
                _logger.LogWarning("Failed to summarize issue {Id}: {Error}", issueId, result.Error);
            }
        }

        return new IssueDetailResult(
            Found: true,
            Issue: issueDto,
            Summary: summary,
            SummaryProvider: summaryProvider,
            SummaryError: summaryError,
            ErrorMessage: null);
    }

    private static (string Owner, string RepoName) ParseRepositoryFullName(string fullName)
    {
        var parts = fullName.Split('/');
        return parts.Length == 2
            ? (parts[0], parts[1])
            : (string.Empty, string.Empty);
    }
}
