using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Olbrasoft.GitHub.Issues.Data.Dtos;
using Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore;

namespace Olbrasoft.GitHub.Issues.Business.Services;

/// <summary>
/// Service for fetching issue details including body from GraphQL and Czech AI summary.
/// Uses two-step process: English summarization → Czech translation.
/// Single responsibility: Orchestrate data retrieval for issue detail page.
/// </summary>
public class IssueDetailService : IIssueDetailService
{
    private readonly GitHubDbContext _dbContext;
    private readonly IGitHubGraphQLClient _graphQLClient;
    private readonly IAiSummarizationService _summarizationService;
    private readonly IAiTranslationService _translationService;
    private readonly ILogger<IssueDetailService> _logger;

    // Cache validity period - regenerate summary if issue was updated after cache
    private static readonly TimeSpan CacheValidityPeriod = TimeSpan.FromDays(7);

    public IssueDetailService(
        GitHubDbContext dbContext,
        IGitHubGraphQLClient graphQLClient,
        IAiSummarizationService summarizationService,
        IAiTranslationService translationService,
        ILogger<IssueDetailService> logger)
    {
        _dbContext = dbContext;
        _graphQLClient = graphQLClient;
        _summarizationService = summarizationService;
        _translationService = translationService;
        _logger = logger;
    }

    public async Task<IssueDetailResult> GetIssueDetailAsync(int issueId, CancellationToken cancellationToken = default)
    {
        var issue = await _dbContext.Issues
            .Include(i => i.Repository)
            .Include(i => i.IssueLabels)
                .ThenInclude(il => il.Label)
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

        var labels = issue.IssueLabels
            .Select(il => new LabelDto(il.Label.Name, il.Label.Color))
            .ToList();

        var issueDto = new IssueDetailDto(
            Id: issue.Id,
            IssueNumber: issue.Number,
            Title: issue.Title,
            IsOpen: issue.IsOpen,
            Url: issue.Url,
            Owner: owner,
            RepoName: repoName,
            RepositoryName: issue.Repository.FullName,
            Body: null,
            Labels: labels);

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

        // Generate Czech AI summary (two-step: summarize → translate)
        string? summary = null;
        string? summaryProvider = null;
        string? summaryError = null;

        // Check cache first
        var isCacheValid = issue.SummaryCachedAt.HasValue &&
                          issue.SummaryCachedAt.Value > issue.GitHubUpdatedAt &&
                          issue.SummaryCachedAt.Value > DateTimeOffset.UtcNow.Subtract(CacheValidityPeriod);

        if (isCacheValid && !string.IsNullOrWhiteSpace(issue.CzechSummary))
        {
            _logger.LogDebug("Using cached Czech summary for issue {Id}", issueId);
            summary = issue.CzechSummary;
            summaryProvider = issue.SummaryProvider;
        }
        else if (!string.IsNullOrWhiteSpace(body))
        {
            // Step 1: Summarize in English
            var summarizeResult = await _summarizationService.SummarizeAsync(body, cancellationToken);
            if (summarizeResult.Success && !string.IsNullOrWhiteSpace(summarizeResult.Summary))
            {
                // Step 2: Translate to Czech (using different provider)
                var translateResult = await _translationService.TranslateToCzechAsync(summarizeResult.Summary, cancellationToken);
                if (translateResult.Success)
                {
                    summary = translateResult.Translation;
                    summaryProvider = $"{summarizeResult.Provider}/{summarizeResult.Model} → {translateResult.Provider}/{translateResult.Model}";

                    // Cache the result
                    await CacheSummaryAsync(issueId, summary!, summaryProvider, cancellationToken);
                }
                else
                {
                    // Translation failed - use English summary as fallback
                    _logger.LogWarning("Translation failed for issue {Id}: {Error}. Using English summary.", issueId, translateResult.Error);
                    summary = summarizeResult.Summary;
                    summaryProvider = $"{summarizeResult.Provider}/{summarizeResult.Model} (EN)";
                }
            }
            else
            {
                summaryError = summarizeResult.Error;
                _logger.LogWarning("Failed to summarize issue {Id}: {Error}", issueId, summarizeResult.Error);
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

    private async Task CacheSummaryAsync(int issueId, string czechSummary, string provider, CancellationToken cancellationToken)
    {
        try
        {
            var issue = await _dbContext.Issues.FindAsync(new object[] { issueId }, cancellationToken);
            if (issue != null)
            {
                issue.CzechSummary = czechSummary;
                issue.SummaryProvider = provider;
                issue.SummaryCachedAt = DateTimeOffset.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogDebug("Cached Czech summary for issue {Id}", issueId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache summary for issue {Id}", issueId);
            // Don't fail the request if caching fails
        }
    }

    private static (string Owner, string RepoName) ParseRepositoryFullName(string fullName)
    {
        var parts = fullName.Split('/');
        return parts.Length == 2
            ? (parts[0], parts[1])
            : (string.Empty, string.Empty);
    }
}
